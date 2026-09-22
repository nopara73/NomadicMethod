using System.Text.Json;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class ExerciseSessionServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedMovementRoundsPreserveSeparateProgressKeepsAndHistory(bool light)
    {
        Exercise[] exercises = [FullyCoveredExercise(1, CanonicalMuscleGroup.ScapularGirdle)];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, light ? WorkoutModifiers.Light : WorkoutModifiers.None);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(3, rounds.Length);
        Assert.Equal(3, rounds.Select(round => round.Id).Distinct().Count());
        Assert.All(rounds, round => Assert.Equal(1, service.GetSelectedExercise(state, round).Id));
        Assert.Equal(3, state.ActiveWorkoutSession!.InitialSelections.Count);
        service.BeginRest(state, rounds[0], DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
        Assert.True(service.KeepPendingRest(state));
        service.RecordOutcome(state, rounds[0], keep: true);
        service.ClearPendingRest(state);
        service.PauseMovement(state, rounds[1], 27_000, pausedByUser: true);
        state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        service = new ExerciseSessionService(exercises, new Random(2));
        service.Initialize(state);
        Assert.Equal(rounds[1].Id, service.GetNextGroup(state)!.Id);
        Assert.Single(state.ActiveWorkoutSession!.Blocks);
        Assert.Equal(27_000, service.GetPendingMovementMillisecondsRemaining(state,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        service.ReconfigureActiveWorkout(state, state.ActiveWorkoutModifiers | WorkoutModifiers.Silence,
            rounds[1].Id);
        Assert.Equal(rounds[1].Id, service.GetNextGroup(state)!.Id);
        Assert.Single(state.Outcomes);
        Assert.Contains(state.ActiveWorkoutSession!.Decisions, decision =>
            decision.SelectionGroupId == rounds[0].SelectionKey && decision.Outcome == ExerciseOutcome.Tick);
        foreach (WorkoutGroup round in rounds.Skip(1))
        {
            service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
            service.RecordOutcome(state, round, keep: round.Id == rounds[2].Id);
            service.ClearPendingRest(state);
        }
        Assert.True(state.WorkoutCompleted);
        WorkoutSessionLog log = state.WorkoutHistory.Single();
        Assert.Equal(3, log.Blocks.Count);
        Assert.Equal(3, log.Decisions.Count);
        Assert.Equal(3, log.Blocks.Select(block => block.WorkoutGroupId).Distinct().Count());
        Assert.Equal(3, log.Decisions.Select(decision => decision.SelectionGroupId).Distinct().Count());
        service.AcknowledgeCompletion(state);
        Assert.Contains(1, state.KeptExerciseRootIdsBySelectionGroupId[rounds[0].SelectionKey]);
        Assert.False(state.KeptExerciseRootIdsBySelectionGroupId.ContainsKey(rounds[1].SelectionKey));
        Assert.All(log.Blocks, block => Assert.Equal(1, block.RootExerciseId));
    }

    [Theory]
    [InlineData(false, 30)]
    [InlineData(true, 29)]
    public void RepeatedAtomicSequencesKeepEveryBlockAndCrossPrimaryPlacement(bool crossPrimary, int placementCount)
    {
        Exercise first = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(1, CanonicalMuscleGroup.ForearmFlexorsAndPronators), 2);
        Exercise second = CloneWithLinkedSequenceMember(FullyCoveredExercise(2,
            crossPrimary ? CanonicalMuscleGroup.ForearmExtensorsAndSupinators :
                CanonicalMuscleGroup.ForearmFlexorsAndPronators), 1);
        var service = new ExerciseSessionService([first, second], new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 60, WorkoutModifiers.None);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(60, rounds.Length);
        Assert.Equal(placementCount, state.ActiveWorkoutSession!.InitialSelections.Count);
        Assert.Equal(60, rounds.Select(round => round.Id).Distinct().Count());
        foreach (WorkoutGroup[] pair in rounds.Chunk(2))
        {
            Assert.Equal(pair[0].SelectionKey, pair[1].SelectionKey);
            Assert.Equal(new[] { 1, 2 }, pair.Select(round => service.GetSelectedExercise(state, round).Id));
            foreach (WorkoutGroup round in pair)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
        }
        Assert.True(state.WorkoutCompleted);
        Assert.Equal(60, state.WorkoutHistory.Single().Blocks.Count);
        Assert.Equal(placementCount, state.WorkoutHistory.Single().Decisions.Count);
    }

    [Fact]
    public void RepeatedMovementsCannotTruncateAnAtomicSequenceToFitTheDuration()
    {
        var service = new ExerciseSessionService(DirectionPairCatalog().Take(2).ToArray(), new Random(1));
        Assert.Throws<InvalidOperationException>(() => service.StartWorkout(new WorkoutState(), 3, WorkoutModifiers.None));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(90)]
    public void DoneWithOnlyOneChoicePerSlotPreservesFeedbackAndCanStartAgain(int minutes)
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups.Select((group, index) =>
            QualifiedForGroup(index + 1, group, 7)).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            KeptExerciseRootIdsBySelectionGroupId = groups.Select((group, index) =>
                (group.Id, ExerciseId: exercises[index].Id)).ToDictionary(
                    entry => entry.Id, entry => new HashSet<int> { entry.ExerciseId }),
        };
        service.StartWorkout(state, minutes, WorkoutModifiers.None);
        while (service.GetNextGroup(state) is { } group)
        {
            service.BeginRest(state, group, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
            if (service.IsIntermediateSequenceBlock(state, group))
                service.AdvanceSequence(state, group);
            else
                service.RecordOutcome(state, group, keep: false);
            service.ClearPendingRest(state);
        }
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string hardWork = JsonSerializer.Serialize(state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        string meaningfulWork = JsonSerializer.Serialize(state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);

        service.AcknowledgeCompletion(state);
        WorkoutState restored = JsonSerializer.Deserialize<WorkoutState>(
            JsonSerializer.Serialize(state))!;
        service.Initialize(restored);

        Assert.Equal(0, restored.ActiveWorkoutMinutes);
        Assert.Empty(restored.Outcomes);
        Assert.Empty(restored.SelectedExerciseIds);
        Assert.Equal(history, JsonSerializer.Serialize(restored.WorkoutHistory));
        Assert.Equal(feedback, JsonSerializer.Serialize(restored.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(keeps, JsonSerializer.Serialize(restored.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(hardWork, JsonSerializer.Serialize(restored.LastHardWorkUnixMillisecondsByPrimaryMuscle));
        Assert.Equal(meaningfulWork, JsonSerializer.Serialize(restored.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle));
        Assert.All(exercises, exercise => Assert.Equal(7, exercise.Score));

        service.PrepareWorkout(restored, minutes, WorkoutModifiers.None);
        Assert.Equal(minutes, service.GetActiveGroups(restored).Count);
        Assert.Equal(groups.Length, service.GetActiveGroups(restored)
            .Select(group => group.SelectionKey).Distinct().Count());
    }

    [Fact]
    public void LeavingRestWithoutAnyReplacementSettlesOnlyOnce()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups.Select((group, index) =>
            QualifiedForGroup(index + 1, group)).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState { CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision };
        service.StartWorkout(state, 30, WorkoutModifiers.None);
        WorkoutGroup first = service.GetNextGroup(state)!;
        int rejectedId = service.GetSelectedExercise(state, first).Id;
        service.BeginRest(state, first, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());

        service.FinishInterruptedWorkout(state);
        service.FinishInterruptedWorkout(state);
        service.Initialize(state);

        WorkoutSessionLog history = Assert.Single(state.WorkoutHistory);
        Assert.Equal(WorkoutSessionStatus.Interrupted, history.Status);
        Assert.Single(history.Blocks);
        Assert.Single(history.Decisions);
        Assert.Equal(-1, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][rejectedId]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        service.PrepareWorkout(state, 30, WorkoutModifiers.None);
        Assert.Equal(30, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void RecoveryLightUsesOnlyDemandPathsSelectableForCurrentModifiers()
    {
        long now = new DateTimeOffset(
            2026, 8, 24, 12, 0, 0, TimeSpan.Zero)
            .ToUnixTimeMilliseconds();
        CanonicalMuscleGroup[] includedMuscles =
        [
            CanonicalMuscleGroup.GlutealExtensors,
            CanonicalMuscleGroup.Chest,
            CanonicalMuscleGroup.ElbowFlexors,
            CanonicalMuscleGroup.Soleus,
            CanonicalMuscleGroup.AbdominalWall,
        ];
        Exercise[] included = includedMuscles
            .Select((muscle, index) => CloneWithMuscularDemand(
                Exercise(
                    index + 1,
                    muscle,
                    0,
                    ExerciseSideSequence.Continuous,
                    ExerciseInsectCompatibility.Compatible),
                NomadicMethod.Models.Exercise.ModerateMuscularDemand))
            .ToArray();
        Exercise filteredFresh = CloneWithMuscularDemand(
            Exercise(
                100,
                CanonicalMuscleGroup.SpinalExtensors,
                0,
                ExerciseSideSequence.Continuous,
                ExerciseInsectCompatibility.Incompatible),
            NomadicMethod.Models.Exercise.ModerateMuscularDemand);
        var service = new ExerciseSessionService(
            [.. included, filteredFresh],
            utcNowProvider: () =>
                DateTimeOffset.FromUnixTimeMilliseconds(now));
        var state = new WorkoutState
        {
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                includedMuscles
                    .Take(4)
                    .ToDictionary(
                        muscle => muscle.ToString(),
                        _ => now - (long)TimeSpan.FromHours(1)
                            .TotalMilliseconds),
        };

        WorkoutRecoveryLightStatus neutral = service.GetRecoveryLightStatus(
            state,
            WorkoutModifiers.None,
            now);
        WorkoutRecoveryLightStatus insect = service.GetRecoveryLightStatus(
            state,
            WorkoutModifiers.Insect,
            now);

        Assert.False(neutral.IsActive);
        Assert.Equal(6, neutral.EligibleMuscleCount);
        Assert.True(insect.IsActive);
        Assert.Equal(5, insect.EligibleMuscleCount);
    }

    [Fact]
    public void MuscleBalanceReplacesAnUnkeptChoiceOnlyWhenEverySlotScoreIsPreserved()
    {
        (WorkoutGroup[] groups, Exercise[] selected, WorkoutState state) =
            CreateThirtyMinuteBalanceFixture();
        WorkoutGroup targetGroup = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors));
        Exercise alternative = CloneWithMuscularDemand(
            Exercise(
                1_001,
                targetGroup.CanonicalGroups.Single(),
                0,
                CanonicalMuscleGroup.PelvicFloorAndPerineum),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        var service = new ExerciseSessionService(
            [.. selected, alternative],
            new AlwaysZeroRandom());

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        Assert.Equal(alternative.Id, state.SelectedExerciseIds[targetGroup.Id]);
        Assert.Equal(0, alternative.Score);
        Assert.Empty(state.ExerciseScoreAdjustmentsBySelectionGroupId);
    }

    [Fact]
    public void MuscleBalanceCanUseAnAtomicSequenceWithoutSplittingItsSlots()
    {
        (WorkoutGroup[] groups, Exercise[] selected, WorkoutState state) =
            CreateThirtyMinuteBalanceFixture();
        WorkoutGroup shoulderAbductors = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors));
        WorkoutGroup rotatorCuff = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.RotatorCuff));
        Exercise root = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                Exercise(
                    1_001,
                    shoulderAbductors.CanonicalGroups.Single(),
                    0,
                    CanonicalMuscleGroup.PelvicFloorAndPerineum),
                1_002),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        Exercise member = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                Exercise(
                    1_002,
                    rotatorCuff.CanonicalGroups.Single()),
                1_001),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        var service = new ExerciseSessionService(
            [.. selected, root, member],
            new AlwaysZeroRandom());

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        Assert.Equal(root.Id, state.SelectedExerciseIds[shoulderAbductors.Id]);
        Assert.Equal(root.Id, state.SelectedExerciseIds[rotatorCuff.Id]);
        WorkoutGroup[] sequenceRounds = service.GetActiveGroups(state)
            .Where(round => round.SelectionKey ==
                new[] { shoulderAbductors, rotatorCuff }
                    .MinBy(group => group.Order)!
                    .Id)
            .ToArray();
        Assert.Equal(2, sequenceRounds.Length);
        Assert.Equal([root.Id, member.Id], sequenceRounds
            .Select(round => round.ExerciseOverrideId));
    }

    [Fact]
    public void MuscleBalanceNeverMovesASelectedKeep()
    {
        (WorkoutGroup[] groups, Exercise[] selected, WorkoutState state) =
            CreateThirtyMinuteBalanceFixture();
        WorkoutGroup targetGroup = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors));
        Exercise current = selected.Single(exercise =>
            exercise.PrimaryCanonicalGroup ==
                CanonicalMuscleGroup.ShoulderAbductors);
        Exercise alternative = CloneWithMuscularDemand(
            Exercise(
                1_001,
                current.PrimaryCanonicalGroup,
                0,
                CanonicalMuscleGroup.PelvicFloorAndPerineum),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        state.KeptExerciseRootIdsBySelectionGroupId[targetGroup.Id] =
            [current.Id];
        var service = new ExerciseSessionService(
            [.. selected, alternative],
            new AlwaysZeroRandom());

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        Assert.Equal(current.Id, state.SelectedExerciseIds[targetGroup.Id]);
        Assert.Contains(
            current.Id,
            state.KeptExerciseRootIdsBySelectionGroupId[targetGroup.Id]);
    }

    [Fact]
    public void MuscleBalanceNeverPromotesARejectedLowerScoreExercise()
    {
        (WorkoutGroup[] groups, Exercise[] selected, WorkoutState state) =
            CreateThirtyMinuteBalanceFixture();
        WorkoutGroup targetGroup = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors));
        Exercise current = selected.Single(exercise =>
            exercise.PrimaryCanonicalGroup ==
                CanonicalMuscleGroup.ShoulderAbductors);
        Exercise rejectedAlternative = CloneWithMuscularDemand(
            Exercise(
                1_001,
                current.PrimaryCanonicalGroup,
                -1,
                CanonicalMuscleGroup.PelvicFloorAndPerineum),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        var service = new ExerciseSessionService(
            [.. selected, rejectedAlternative],
            new AlwaysZeroRandom());

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        Assert.Equal(current.Id, state.SelectedExerciseIds[targetGroup.Id]);
        Assert.Equal(-1, rejectedAlternative.Score);
    }

    [Fact]
    public void LongWorkoutBalanceUsesTheActualRepeatedSetAllocation()
    {
        (WorkoutGroup[] groups, Exercise[] selected, WorkoutState state) =
            CreateThirtyMinuteBalanceFixture();
        WorkoutGroup targetGroup = groups.Single(group =>
            group.CanonicalGroups.Contains(CanonicalMuscleGroup.HipFlexors));
        Exercise alternative = CloneWithMuscularDemand(
            Exercise(
                1_001,
                targetGroup.CanonicalGroups.Single(),
                0,
                CanonicalMuscleGroup
                    .AnteriorLateralLowerLegAndDorsalFoot),
            NomadicMethod.Models.Exercise.MinimumMuscularDemand);
        state.LastWorkoutMinutes = 45;
        var service = new ExerciseSessionService(
            [.. selected, alternative],
            new AlwaysZeroRandom());

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Equal(alternative.Id, state.SelectedExerciseIds[targetGroup.Id]);
        Assert.Equal(
            2,
            service.GetActiveGroups(state).Count(round =>
                round.SelectionKey == targetGroup.Id));
    }

    [Fact]
    public void UnreviewedCatalogCannotSilentlyTreatEnabledModifierAsOff()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        Assert.False(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.Throws<InvalidOperationException>(() =>
            service.StartWorkout(state, 3, WorkoutModifiers.Insect));
    }

    [Fact]
    public void InsectSelectionFiltersBeforeScoreAndCoverageRanking()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));

        var normal = new WorkoutState();
        service.StartWorkout(normal, 3, WorkoutModifiers.None);
        Assert.All(service.GetActiveGroups(normal), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Incompatible,
                service.GetSelectedExercise(normal, group).InsectCompatibility));

        var insect = new WorkoutState();
        service.StartWorkout(insect, 3, WorkoutModifiers.Insect);
        Assert.True(WorkoutModifierPolicy.IsCatalogMetadataComplete(exercises));
        Assert.All(service.GetActiveGroups(insect), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(insect, group).InsectCompatibility));
    }

    [Fact]
    public void MirrorPreferenceBreaksTiesButNeverOverridesARealVote()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] agnostic = groups
            .Select((group, index) => QualifiedForGroup(
                20_000 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] benefitsGreatly = agnostic
            .Select(exercise => CloneWithMirrorRelationship(
                exercise,
                exercise.Id + 1,
                ExerciseMirrorRelationship.BenefitsGreatly))
            .ToArray();
        Exercise[] exercises = agnostic.Concat(benefitsGreatly).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var tiedState = new WorkoutState();

        service.StartWorkout(tiedState, 3, WorkoutModifiers.Mirror);

        Assert.All(service.GetActiveGroups(tiedState), group => Assert.Equal(
            ExerciseMirrorRelationship.BenefitsGreatly,
            service.GetSelectedExercise(tiedState, group).MirrorRelationship));

        foreach (Exercise exercise in agnostic)
        {
            exercise.Score = 1;
        }
        var votedState = new WorkoutState();
        service.StartWorkout(votedState, 3, WorkoutModifiers.Mirror);

        Assert.All(service.GetActiveGroups(votedState), group => Assert.Equal(
            ExerciseMirrorRelationship.Agnostic,
            service.GetSelectedExercise(votedState, group).MirrorRelationship));
    }

    [Fact]
    public void WallPreferenceBreaksTiesButNeverOverridesARealVote()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                21_000 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        Exercise[] exercises = ordinary.Concat(wallRequired).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var tiedState = new WorkoutState();

        service.StartWorkout(tiedState, 3, WorkoutModifiers.Wall);

        Assert.All(service.GetActiveGroups(tiedState), group => Assert.True(
            service.GetSelectedExercise(tiedState, group).WallRequired));

        foreach (Exercise exercise in ordinary)
        {
            exercise.Score = 1;
        }
        var votedState = new WorkoutState();
        service.StartWorkout(votedState, 3, WorkoutModifiers.Wall);

        Assert.All(service.GetActiveGroups(votedState), group => Assert.False(
            service.GetSelectedExercise(votedState, group).WallRequired));
    }

    [Fact]
    public void MidWorkoutModifierChangeReplacesIncompatibleCurrentMovementAndReplansFutureSelections()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                22_000 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        Exercise[] exercises = ordinary.Concat(wallRequired).ToArray();
        var service = new ExerciseSessionService(exercises, new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.Wall);
        WorkoutGroup completedGroup = service.GetNextGroup(state)!;
        Exercise completedExercise = service.GetSelectedExercise(state, completedGroup);
        Assert.True(completedExercise.WallRequired);
        service.RecordOutcome(state, completedGroup, keep: true);

        WorkoutGroup currentGroup = service.GetNextGroup(state)!;
        Exercise currentExercise = service.GetSelectedExercise(state, currentGroup);
        Assert.True(currentExercise.WallRequired);
        const long movementRemaining = 28_000;
        service.BeginMovement(
            state,
            currentGroup,
            movementRemaining,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + movementRemaining);
        int[] keptBefore = state.LastKeptExerciseIds.Order().ToArray();
        string[] initialSelectionGroups = state.ActiveWorkoutSession!
            .InitialSelections
            .Select(selection => selection.SelectionGroupId)
            .ToArray();

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.None,
            currentGroup.Id);

        Assert.Equal(WorkoutModifiers.None, state.ActiveWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        WorkoutGroup replacementGroup = service.GetNextGroup(state)!;
        Exercise replacementExercise = service.GetSelectedExercise(
            state,
            replacementGroup);
        Assert.Equal(currentGroup.SelectionKey, replacementGroup.SelectionKey);
        Assert.NotEqual(currentExercise.Id, replacementExercise.Id);
        Assert.False(replacementExercise.WallRequired);
        Assert.Null(state.PendingMovementGroupId);
        Assert.Equal(0, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[completedGroup.Id]);
        Assert.Equal(keptBefore, state.LastKeptExerciseIds.Order());
        Assert.Empty(state.ExerciseScoreAdjustmentsByPhase);
        Assert.Equal(
            completedExercise.Id,
            state.SelectedExerciseIds[completedGroup.SelectionKey]);
        Assert.All(
            service.GetActiveGroups(state).Where(group =>
                !state.Outcomes.ContainsKey(group.Id)),
            group => Assert.False(
                service.GetSelectedExercise(state, group).WallRequired));
        Assert.Equal(initialSelectionGroups, state.ActiveSelectionGroupOrder);

        WorkoutModifierChangeLog change = Assert.Single(
            state.ActiveWorkoutSession.ModifierChanges);
        Assert.Equal(WorkoutModifiers.Wall, change.PreviousModifiers);
        Assert.Equal(WorkoutModifiers.None, change.NewModifiers);
        Assert.Empty(change.ProtectedSelectionGroupId);
        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
        Assert.Equal(groups.Length, change.PlannedSelections.Count);
        Assert.Equal(
            WorkoutModifiers.Wall,
            state.ActiveWorkoutSession.Modifiers);

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        var restoredService = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom());
        restoredService.Initialize(restored);
        WorkoutGroup restoredCurrent = restoredService.GetNextGroup(restored)!;
        Assert.Equal(replacementGroup.Id, restoredCurrent.Id);
        Assert.Equal(replacementExercise.Id, restoredService.GetSelectedExercise(
            restored,
            restoredCurrent).Id);
        Assert.Null(restored.ActiveModifierProtectedSelectionGroupId);
        Assert.Null(restored.PendingMovementGroupId);
        Assert.Single(restored.ActiveWorkoutSession!.ModifierChanges);

        restoredService.RecordOutcome(restored, restoredCurrent, keep: false);

        Assert.Null(restored.ActiveModifierProtectedSelectionGroupId);
        Assert.False(restoredService.GetSelectedExercise(
            restored,
            restoredService.GetNextGroup(restored)!).WallRequired);
    }

    [Fact]
    public void InsectTransitionRemovesAnUnfinishedStructurallyUnavailableSlot()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);
        WorkoutGroup[] initialRounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup unavailableRound = initialRounds.First(round =>
            !WorkoutModifierPolicy.IsSelectionGroupAvailable(
                round,
                WorkoutModifiers.Insect));
        foreach (WorkoutGroup priorRound in initialRounds.TakeWhile(round =>
                     round.Id != unavailableRound.Id))
        {
            service.RecordOutcome(state, priorRound, keep: false);
        }
        Assert.Equal(unavailableRound.Id, service.GetNextGroup(state)?.Id);
        service.BeginMovement(
            state,
            unavailableRound,
            millisecondsRemaining: 28_000,
            endsAtUnixMilliseconds: 1_800_000_028_000);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Insect,
            unavailableRound.Id);

        WorkoutGroup[] replannedRounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(30, replannedRounds.Length);
        Assert.DoesNotContain(replannedRounds, round =>
            round.SelectionKey == unavailableRound.SelectionKey);
        Assert.Null(state.PendingMovementGroupId);
        Assert.All(replannedRounds.Where(round =>
            !state.Outcomes.ContainsKey(round.Id)), round => Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(state, round).InsectCompatibility));
    }

    [Fact]
    public void InsectTransitionRetainsCompletedUnavailableWorkAcrossRestart()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);
        WorkoutGroup[] initialRounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup unavailableRound = initialRounds
            .Where(round => !WorkoutModifierPolicy.IsSelectionGroupAvailable(
                round,
                WorkoutModifiers.Insect))
            .First(round => round.Order < initialRounds.Length);
        foreach (WorkoutGroup round in initialRounds.TakeWhile(round =>
                     round.Order <= unavailableRound.Order))
        {
            service.RecordOutcome(state, round, keep: false);
        }
        WorkoutGroup currentRound = Assert.IsType<WorkoutGroup>(
            service.GetNextGroup(state));

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Insect,
            currentRound.Id);

        Assert.Contains(
            unavailableRound.SelectionKey,
            state.ActiveModifierRetainedSelectionGroupIds);
        WorkoutGroup retainedRound = service.GetActiveGroups(state).Single(round =>
            round.SelectionKey == unavailableRound.SelectionKey);
        int retainedExerciseId = service.GetSelectedExercise(
            state,
            retainedRound).Id;
        var restored = JsonSerializer.Deserialize<WorkoutState>(
            JsonSerializer.Serialize(state))!;
        var restoredService = new ExerciseSessionService(
            exercises,
            new Random(1));

        restoredService.Initialize(restored);

        Assert.Contains(
            unavailableRound.SelectionKey,
            restored.ActiveModifierRetainedSelectionGroupIds);
        Assert.Equal(30, restoredService.GetActiveGroups(restored).Count);
        WorkoutGroup restoredRetainedRound = restoredService
            .GetActiveGroups(restored)
            .Single(round => round.SelectionKey ==
                unavailableRound.SelectionKey);
        Assert.Equal(
            retainedExerciseId,
            restoredService.GetSelectedExercise(
                restored,
                restoredRetainedRound).Id);
    }

    [Fact]
    public void ReenablingMirrorRestoresTheCurrentMirrorProfileSelection()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                22_300 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] mirrorOnly = ordinary
            .Select(exercise => CloneWithMirrorRelationship(
                exercise,
                exercise.Id + 1,
                ExerciseMirrorRelationship.MirrorOnly))
            .ToArray();
        var service = new ExerciseSessionService(
            ordinary.Concat(mirrorOnly).ToArray(),
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Mirror);
        WorkoutGroup initialGroup = service.GetNextGroup(state)!;
        Exercise initialMirrorExercise = service.GetSelectedExercise(
            state,
            initialGroup);
        Assert.Equal(
            ExerciseMirrorRelationship.MirrorOnly,
            initialMirrorExercise.MirrorRelationship);
        service.BeginMovement(
            state,
            initialGroup,
            28_000,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 28_000);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.None,
            initialGroup.Id);

        WorkoutGroup ordinaryGroup = service.GetNextGroup(state)!;
        Exercise ordinaryExercise = service.GetSelectedExercise(
            state,
            ordinaryGroup);
        Assert.Equal(
            ExerciseMirrorRelationship.Agnostic,
            ordinaryExercise.MirrorRelationship);
        Assert.Null(state.PendingMovementGroupId);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Mirror,
            ordinaryGroup.Id);

        WorkoutGroup restoredGroup = service.GetNextGroup(state)!;
        Exercise restoredMirrorExercise = service.GetSelectedExercise(
            state,
            restoredGroup);
        Assert.Equal(initialGroup.SelectionKey, restoredGroup.SelectionKey);
        Assert.Equal(initialMirrorExercise.Id, restoredMirrorExercise.Id);
        Assert.Equal(
            ExerciseMirrorRelationship.MirrorOnly,
            restoredMirrorExercise.MirrorRelationship);
        Assert.Null(state.PendingMovementGroupId);
        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
        Assert.Equal(2, state.ActiveWorkoutSession!.ModifierChanges.Count);
    }

    [Fact]
    public void LightCanReplanCurrentWorkAndThenRestoreTheRegularProfile()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] hard = groups
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(22_600 + index * 2, group),
                muscularDemand: 2))
            .ToArray();
        Exercise[] easy = groups
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(22_601 + index * 2, group),
                muscularDemand: 0))
            .ToArray();
        var service = new ExerciseSessionService(
            hard.Concat(easy).ToArray(),
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup regularGroup = service.GetNextGroup(state)!;
        Exercise regularExercise = service.GetSelectedExercise(state, regularGroup);
        Assert.Equal(2, regularExercise.MuscularDemand);
        service.BeginMovement(
            state,
            regularGroup,
            28_000,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 28_000);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Light,
            regularGroup.Id);

        WorkoutGroup lightGroup = service.GetNextGroup(state)!;
        Exercise lightExercise = service.GetSelectedExercise(state, lightGroup);
        Assert.Equal(regularGroup.SelectionKey, lightGroup.SelectionKey);
        Assert.Equal(0, lightExercise.MuscularDemand);
        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(WorkoutModifiers.Light, state.ActiveWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.Null(state.PendingMovementGroupId);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.None,
            lightGroup.Id);

        WorkoutGroup restoredGroup = service.GetNextGroup(state)!;
        Assert.Equal(regularGroup.SelectionKey, restoredGroup.SelectionKey);
        Assert.Equal(
            regularExercise.Id,
            service.GetSelectedExercise(state, restoredGroup).Id);
        Assert.False(state.ActiveWorkoutIsLightDay);
        Assert.Equal(WorkoutModifiers.None, state.ActiveWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.Equal(2, state.ActiveWorkoutSession!.ModifierChanges.Count);
        Assert.Equal(
            WorkoutModifiers.Light,
            state.ActiveWorkoutSession.ModifierChanges[0].NewModifiers);
        Assert.Equal(
            WorkoutModifiers.None,
            state.ActiveWorkoutSession.ModifierChanges[1].NewModifiers);
    }

    [Fact]
    public void CompatibleModifierTransitionPreservesCurrentMovementCheckpoint()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                22_500 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        var service = new ExerciseSessionService(
            ordinary.Concat(wallRequired).ToArray(),
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Wall);
        WorkoutGroup currentGroup = service.GetNextGroup(state)!;
        Exercise currentExercise = service.GetSelectedExercise(state, currentGroup);
        const long movementRemaining = 28_000;
        service.BeginMovement(
            state,
            currentGroup,
            movementRemaining,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + movementRemaining);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Wall | WorkoutModifiers.HardFloor,
            currentGroup.Id);

        Assert.Equal(currentGroup.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(currentExercise.Id, service.GetSelectedExercise(
            state,
            service.GetNextGroup(state)!).Id);
        Assert.Equal(currentGroup.Id, state.PendingMovementGroupId);
        Assert.Equal(movementRemaining, state.PendingMovementMillisecondsRemaining);
        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
        Assert.Empty(Assert.Single(
            state.ActiveWorkoutSession!.ModifierChanges)
            .ProtectedSelectionGroupId);
    }

    [Fact]
    public void InitializationReplacesLegacyProtectedIncompatibleMovement()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                22_700 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        Exercise[] exercises = ordinary.Concat(wallRequired).ToArray();
        var service = new ExerciseSessionService(exercises, new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Wall);
        WorkoutGroup currentGroup = service.GetNextGroup(state)!;
        service.BeginMovement(
            state,
            currentGroup,
            28_000,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 28_000);

        // This is the persisted shape produced by the previous implementation:
        // the modifier profile changed, but the now-incompatible current root
        // and its partial timer were retained through a protection exception.
        state.ActiveWorkoutModifiers = WorkoutModifiers.None;
        state.LastWorkoutModifiers = WorkoutModifiers.None;
        foreach ((WorkoutGroup group, Exercise wallExercise) in
                 groups.Zip(wallRequired))
        {
            state.SelectedExerciseIds[group.Id] = wallExercise.Id;
        }
        state.ActiveModifierProtectedSelectionGroupId = currentGroup.SelectionKey;

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        var restoredService = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom());

        restoredService.Initialize(restored);

        WorkoutGroup replacementGroup = restoredService.GetNextGroup(restored)!;
        Assert.False(restoredService.GetSelectedExercise(
            restored,
            replacementGroup).WallRequired);
        Assert.Null(restored.ActiveModifierProtectedSelectionGroupId);
        Assert.Null(restored.PendingMovementGroupId);
        Assert.Equal(0, restored.PendingMovementMillisecondsRemaining);
    }

    [Fact]
    public void InitializationClearsLegacyCompatibleProtectionWithoutLosingCheckpoint()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                22_900 + index,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        var service = new ExerciseSessionService(
            ordinary,
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup currentGroup = service.GetNextGroup(state)!;
        Exercise currentExercise = service.GetSelectedExercise(state, currentGroup);
        const long movementRemaining = 28_000;
        service.BeginMovement(
            state,
            currentGroup,
            movementRemaining,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + movementRemaining);

        // Previous builds persisted a compatible current selection as a
        // protection exception. It is safe to remove that exception without
        // discarding the still-valid movement checkpoint.
        state.ActiveModifierProtectedSelectionGroupId = currentGroup.SelectionKey;

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        var restoredService = new ExerciseSessionService(
            ordinary,
            new AlwaysZeroRandom());

        restoredService.Initialize(restored);

        Assert.Null(restored.ActiveModifierProtectedSelectionGroupId);
        Assert.Equal(currentGroup.Id, restoredService.GetNextGroup(restored)!.Id);
        Assert.Equal(currentExercise.Id, restoredService.GetSelectedExercise(
            restored,
            restoredService.GetNextGroup(restored)!).Id);
        Assert.Equal(currentGroup.Id, restored.PendingMovementGroupId);
        Assert.Equal(
            movementRemaining,
            restored.PendingMovementMillisecondsRemaining);
    }

    [Fact]
    public void CompatibleModifierTransitionPreservesRepeatedSelectionWithoutProtection()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                23_000 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        var service = new ExerciseSessionService(
            ordinary.Concat(wallRequired).ToArray(),
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.Wall);
        WorkoutGroup[] initialRounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup firstRepeatedSet = initialRounds.First(round =>
            round.SetCount > 1 && round.SetNumber == 1);
        foreach (WorkoutGroup priorRound in initialRounds.TakeWhile(round =>
                     round.Id != firstRepeatedSet.Id))
        {
            if (service.IsIntermediateSequenceBlock(state, priorRound))
            {
                service.AdvanceSequence(state, priorRound);
            }
            else
            {
                service.RecordOutcome(state, priorRound, keep: false);
            }
        }
        Assert.Equal(firstRepeatedSet.Id, service.GetNextGroup(state)!.Id);
        Exercise protectedExercise = service.GetSelectedExercise(
            state,
            firstRepeatedSet);
        Assert.True(protectedExercise.WallRequired);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.Wall | WorkoutModifiers.HardFloor,
            firstRepeatedSet.Id);
        service.AdvanceSequence(state, firstRepeatedSet);

        WorkoutGroup secondSet = service.GetNextGroup(state)!;
        Assert.Equal(firstRepeatedSet.SelectionKey, secondSet.SelectionKey);
        Assert.Equal(2, secondSet.SetNumber);
        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
        Assert.Equal(
            protectedExercise.Id,
            service.GetSelectedExercise(state, secondSet).Id);

        service.RecordOutcome(state, secondSet, keep: false);

        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
    }

    [Fact]
    public void IncompatibleModifierTransitionReplacesCurrentRepeatedSet()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] ordinary = groups
            .Select((group, index) => QualifiedForGroup(
                23_500 + index * 2,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible))
            .ToArray();
        Exercise[] wallRequired = ordinary
            .Select(exercise => CloneWithWallRequirement(
                exercise,
                exercise.Id + 1,
                wallRequired: true))
            .ToArray();
        var service = new ExerciseSessionService(
            ordinary.Concat(wallRequired).ToArray(),
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.Wall);
        WorkoutGroup[] initialRounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup firstRepeatedSet = initialRounds.First(round =>
            round.SetCount > 1 && round.SetNumber == 1);
        foreach (WorkoutGroup priorRound in initialRounds.TakeWhile(round =>
                     round.Id != firstRepeatedSet.Id))
        {
            if (service.IsIntermediateSequenceBlock(state, priorRound))
            {
                service.AdvanceSequence(state, priorRound);
            }
            else
            {
                service.RecordOutcome(state, priorRound, keep: false);
            }
        }
        Dictionary<string, ExerciseOutcome> completedBefore = state.Outcomes
            .ToDictionary(entry => entry.Key, entry => entry.Value);
        service.BeginMovement(
            state,
            firstRepeatedSet,
            28_000,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 28_000);

        service.ReconfigureActiveWorkout(
            state,
            WorkoutModifiers.None,
            firstRepeatedSet.Id);

        WorkoutGroup replacementRound = service.GetNextGroup(state)!;
        Assert.Equal(firstRepeatedSet.SelectionKey, replacementRound.SelectionKey);
        Assert.False(service.GetSelectedExercise(state, replacementRound).WallRequired);
        Assert.Equal(completedBefore, state.Outcomes);
        Assert.Null(state.PendingMovementGroupId);
        Assert.Null(state.ActiveModifierProtectedSelectionGroupId);
    }

    [Fact]
    public void FullyReviewedCatalogAlwaysHonorsEnabledModifier()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups.SelectMany((group, index) => new[]
        {
            QualifiedForGroup(
                1 + index,
                group,
                insectCompatibility: ExerciseInsectCompatibility.Compatible),
            QualifiedForGroup(
                101 + index,
                group,
                score: 100,
                insectCompatibility: ExerciseInsectCompatibility.Incompatible),
        }).ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.All(service.GetActiveGroups(state), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(state, group).InsectCompatibility));
    }

    [Fact]
    public void PreHardFloorStateKeepsSilenceRelaxedWhileAddingHardFloorDefault()
    {
        var service = new ExerciseSessionService(ReviewedInsectCatalog(), new Random(1));
        var state = new WorkoutState
        {
            Version = 8,
            LastWorkoutModifiers = WorkoutModifiers.None,
        };

        service.Initialize(state);

        Assert.Equal(30, state.Version);
        Assert.Equal(
            WorkoutModifiers.HardFloor |
                WorkoutModifiers.UpperBodyClothing,
            state.LastWorkoutModifiers);
    }

    [Fact]
    public void BinaryMirrorStateDoesNotGuessMirrorHeightDuringMigration()
    {
        Exercise[] exercises = ReviewedInsectCatalog();
        WorkoutGroup group = MassGroupingTaxonomy.GetResolution(3).Groups[0];
        Exercise selected = exercises.First(exercise =>
            WorkoutCoveragePolicy.IsSelectable(exercise, group));
        var state = new WorkoutState
        {
            Version = 11,
            LastWorkoutModifiers = WorkoutModifiers.Insect |
                WorkoutModifiers.Mirror,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [group.Id] = selected.Id,
                [$"p4|{group.Id}"] = selected.Id,
            },
        };
        var service = new ExerciseSessionService(exercises, new Random(1));

        service.Initialize(state);

        Assert.Equal(30, state.Version);
        Assert.Equal(
            WorkoutModifiers.Insect |
                WorkoutModifiers.HardFloor |
                WorkoutModifiers.UpperBodyClothing,
            state.LastWorkoutModifiers);
        Assert.Equal(MirrorEquipment.None,
            WorkoutModifierPolicy.GetMirrorEquipment(state.LastWorkoutModifiers));
        Assert.DoesNotContain(state.SelectedExerciseIds.Keys,
            key => key.StartsWith("p4|", StringComparison.Ordinal));
        Assert.Equal(selected.Id, state.SelectedExerciseIds[group.Id]);
    }

    [Fact]
    public void ModifierProfilesShareKeepsWithoutForgettingExcludedExercises()
    {
        var service = new ExerciseSessionService(
            ReviewedInsectCatalog(),
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        int[] keptIds = service.GetActiveGroups(state)
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        foreach (WorkoutGroup group in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.Equal(keptIds.Order(), state.LastKeptExerciseIds.Order());
        Assert.All(service.GetActiveGroups(state), group =>
            Assert.Equal(
                ExerciseInsectCompatibility.Compatible,
                service.GetSelectedExercise(state, group).InsectCompatibility));

        service.FinishInterruptedWorkout(state);
        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            keptIds.Order(),
            service.GetActiveGroups(state)
                .Select(group => service.GetSelectedExercise(state, group).Id)
                .Order());
    }

    [Fact]
    public void WarmupDownvoteReselectsAlternativeWithoutGlobalExclusion()
    {
        Exercise[] exercises = ThreeGroupCatalog(
            ExerciseInsectCompatibility.Compatible);
        foreach (Exercise exercise in exercises)
        {
            exercise.Score = 0;
        }
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int rejectedExerciseId = service.GetSelectedExercise(state, groups[0]).Id;

        service.RecordOutcome(state, groups[0], keep: false);
        foreach (WorkoutGroup group in groups.Skip(1))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);

        Assert.DoesNotContain(rejectedExerciseId, state.SelectedExerciseIds.Values);
        Assert.Empty(state.NextWorkoutExcludedExerciseIds);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                rejectedExerciseId]);
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);

        Assert.Empty(state.NextWorkoutExcludedExerciseIds);
        Assert.NotEqual(
            rejectedExerciseId,
            service.GetSelectedExercise(state, service.GetActiveGroups(state)[0]).Id);
    }

    [Fact]
    public void DemandOrderedWarmupUsesItsActualPhaseWhenSelectingNextWorkout()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        WorkoutGroup target = groups.Single(group =>
            group.Id == "r30.spinal-extensors");
        Exercise rejected = CloneWithMuscularDemand(
            QualifiedForGroup(1000, target),
            muscularDemand: 0);
        Exercise alternative = CloneWithMuscularDemand(
            QualifiedForGroup(1001, target),
            muscularDemand: 0);
        Exercise[] fixedExercises = groups
            .Where(group => group.Id != target.Id)
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(index + 1, group),
                muscularDemand: 1))
            .ToArray();
        var service = new ExerciseSessionService(
            [rejected, alternative, .. fixedExercises],
            new AlwaysZeroRandom());
        var selectedExerciseIds = fixedExercises.ToDictionary(
            exercise => MassGroupingTaxonomy.GetGroup(
                30,
                exercise.PrimaryCanonicalGroup).Id,
            exercise => exercise.Id,
            StringComparer.Ordinal);
        selectedExerciseIds[target.Id] = rejected.Id;
        var keeps = selectedExerciseIds.ToDictionary(
            entry => entry.Key,
            entry => new HashSet<int> { entry.Value },
            StringComparer.Ordinal);
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = selectedExerciseIds,
            KeptExerciseRootIdsBySelectionGroupId = keeps,
            ExerciseScoreAdjustmentsByPhase = new()
            {
                [WorkoutExercisePhase.Warmup] = new()
                {
                    [rejected.Id] = -1,
                },
            },
        };

        service.StartWorkout(state, 60, WorkoutModifiers.None);

        Assert.Equal(alternative.Id, state.SelectedExerciseIds[target.Id]);
        WorkoutGroup finalTargetBlock = service.GetActiveGroups(state)
            .Last(group => group.SelectionKey == target.Id);
        Assert.Equal(
            WorkoutExercisePhase.Warmup,
            WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                finalTargetBlock.Order));
    }

    [Fact]
    public void ShortWorkoutKeepsCarryIntoTheMatchingLongWorkoutSlots()
    {
        var service = new ExerciseSessionService(
            ReviewedInsectCatalog(),
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.Insect);
        int[] keptExerciseIds = service.GetActiveGroups(state)
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        foreach (WorkoutGroup group in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.AcknowledgeCompletion(state);

        service.StartWorkout(state, 45, WorkoutModifiers.Insect);

        Assert.Equal(keptExerciseIds.Order(), state.LastKeptExerciseIds.Order());
        Assert.All(keptExerciseIds, keptExerciseId => Assert.Contains(
            state.KeptExerciseRootIdsBySelectionGroupId
                .Where(entry => entry.Key.StartsWith("r30.", StringComparison.Ordinal))
                .SelectMany(entry => entry.Value),
            rootId => rootId == keptExerciseId));
        int distinctSelectionCount = service.GetActiveGroups(state)
            .Select(group => group.SelectionKey)
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.Equal(
            45 - distinctSelectionCount,
            state.ActiveExtraSetSelectionGroupIds.Count);
    }

    [Fact]
    public void KeepAndDownvoteForSameExerciseRemainIndependentAcrossPhases()
    {
        WorkoutGroup[] shortGroups = MassGroupingTaxonomy
            .GetResolution(3)
            .Groups
            .ToArray();
        WorkoutGroup[] longGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        const CanonicalMuscleGroup primary = CanonicalMuscleGroup.AbdominalWall;
        WorkoutGroup shortSlot = MassGroupingTaxonomy.GetGroup(3, primary);
        WorkoutGroup longSlot = MassGroupingTaxonomy.GetGroup(30, primary);
        Exercise shared = FullyCoveredExercise(1, primary, score: 10);
        Exercise[] longAlternatives = longGroups
            .Select((group, index) => QualifiedForGroup(
                100 + index,
                group))
            .ToArray();
        Exercise[] shortAlternatives = shortGroups
            .Select((group, index) => QualifiedForGroup(
                200 + index,
                group))
            .ToArray();
        var service = new ExerciseSessionService(
            [shared, .. longAlternatives, .. shortAlternatives],
            new AlwaysZeroRandom());
        var state = new WorkoutState
        {
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [shortSlot.Id] = [shared.Id],
            },
        };

        service.StartWorkout(state, 60, WorkoutModifiers.None);
        WorkoutGroup longRound = service.GetActiveGroups(state)
            .Last(round => round.SelectionKey == longSlot.Id);
        Assert.Equal(shared.Id, service.GetSelectedExercise(state, longRound).Id);

        CompleteRoundsBefore(service, state, longRound);
        service.RecordOutcome(state, longRound, keep: false);

        Assert.Contains(
            shared.Id,
            state.KeptExerciseRootIdsBySelectionGroupId[shortSlot.Id]);
        Assert.Contains(
            shared.Id,
            state.KeptExerciseRootIdsBySelectionGroupId.GetValueOrDefault(
                longSlot.Id) ?? []);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][
                shared.Id]);
        Assert.DoesNotContain(
            WorkoutExercisePhase.Warmup,
            state.ExerciseScoreAdjustmentsByPhase.Keys);
        Assert.DoesNotContain(
            WorkoutExercisePhase.Fatigued,
            state.ExerciseScoreAdjustmentsByPhase.Keys);
        Assert.Contains(shared.Id, state.LastKeptExerciseIds);
        Assert.Equal(10, shared.Score);

        service.FinishInterruptedWorkout(state);
        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(shared.Id, state.SelectedExerciseIds[shortSlot.Id]);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhase.PeakPerformance][
                shared.Id]);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    [InlineData(30)]
    public void EveryResolutionSelectsExactlyOneDistinctExercisePerScheduledGroup(
        int minutes)
    {
        WorkoutGroup[] resolutionGroups = MassGroupingTaxonomy
            .GetResolution(minutes)
            .Groups
            .ToArray();
        Exercise[] exercises = resolutionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int[] exerciseIds = groups
            .Select(group => service.GetSelectedExercise(state, group).Id)
            .ToArray();
        Assert.Equal(minutes, groups.Length);
        Assert.Equal(minutes, exerciseIds.Length);
        Assert.Equal(minutes, exerciseIds.Distinct().Count());
    }

    [Fact]
    public void WorkoutScheduleOrdersDemandZeroThenTwoThenOneBeforeMuscleOrder()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(5).Groups
            .ToArray();
        int[] demandByMuscleOrder = [1, 0, 2, 0, 1];
        Exercise[] exercises = groups
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(index + 1, group),
                demandByMuscleOrder[index]))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 5, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string[] expectedSelectionOrder = groups
            .OrderBy(group => WorkoutSchedulePolicy.GetMuscularDemandPriority(
                exercises[group.Order - 1].MuscularDemand))
            .ThenBy(group => group.Order)
            .Select(group => group.Id)
            .ToArray();
        Assert.Equal(expectedSelectionOrder, rounds.Select(round => round.SelectionKey));
        Assert.Equal(
            [0, 0, 2, 1, 1],
            rounds.Select(round =>
                service.GetSelectedExercise(state, round).MuscularDemand));
        Assert.Equal(
            expectedSelectionOrder,
            state.ActiveWorkoutSession!.InitialSelections.Select(selection =>
                selection.SelectionGroupId));

        Dictionary<string, WorkoutSelectionSnapshot> snapshotsByGroup = state
            .ActiveWorkoutSession.InitialSelections
            .ToDictionary(
                selection => selection.SelectionGroupId,
                StringComparer.Ordinal);
        state.ActiveWorkoutSession.InitialSelections = groups
            .Select(group => snapshotsByGroup[group.Id])
            .ToList();
        Assert.Equal(
            groups.Select(group => group.Id),
            service.GetActiveGroups(state).Select(round => round.SelectionKey));
    }

    [Fact]
    public void MixedDemandSequenceUsesItsHighestDemandAndRemainsAtomic()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups
            .ToArray();
        Exercise root = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(1, groups[0]),
                linkedMemberExerciseId: 2),
            muscularDemand: 0);
        Exercise member = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(2, groups[1]),
                linkedMemberExerciseId: 1),
            muscularDemand: 2);
        Exercise easyStandalone = CloneWithMuscularDemand(
            QualifiedForGroup(3, groups[2]),
            muscularDemand: 0);
        var service = new ExerciseSessionService(
            [root, member, easyStandalone],
            new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(
            [groups[2].Id, groups[0].Id, groups[0].Id],
            rounds.Select(round => round.SelectionKey));
        Assert.Equal(
            [easyStandalone.Id, root.Id, member.Id],
            rounds.Select(round => service.GetSelectedExercise(state, round).Id));
        Assert.Equal(2, WorkoutSchedulePolicy.GetSequenceMuscularDemand(
            root,
            new Dictionary<int, Exercise>
            {
                [root.Id] = root,
                [member.Id] = member,
            }));
    }

    [Fact]
    public void ThirtyMinuteWorkoutSelectsOneDistinctEligibleExercisePerGroup()
    {
        Exercise[] exercises = MassGroupingTaxonomy.GetResolution(30).Groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Equal(30, groups.Length);
        Assert.Equal(30, selected.Select(exercise => exercise.Id).Distinct().Count());
        Assert.All(groups.Zip(selected), pair =>
            Assert.Contains(pair.Second.PrimaryCanonicalGroup, pair.First.CanonicalGroups));
    }

    [Fact]
    public void GlobalLineupUsesAtMostOneAliasOfTheSameSessionMovement()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise root = CloneWithMuscularDemand(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            FullyCoveredExercise(2, groups[0].CanonicalGroups.First(), 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [root, alias, middle, last],
            new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Single(selected, exercise => exercise.Id is 1 or 2);
        Assert.Equal(
            selected.Length,
            selected
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
    }

    [Theory]
    [InlineData(45, 1, 2)]
    [InlineData(60, 2, 2)]
    [InlineData(90, 3, 3)]
    public void LongWorkoutsRepeatTheThirtyMinuteLineupBySet(
        int minutes,
        int firstHalfSets,
        int secondHalfSets)
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(minutes, rounds.Length);
        Assert.Equal(Enumerable.Range(1, minutes), rounds.Select(round => round.Order));
        Assert.Equal(minutes, rounds.Select(round => round.Id).Distinct().Count());

        for (int index = 0; index < selectionGroups.Length; index++)
        {
            WorkoutGroup selectionGroup = selectionGroups[index];
            WorkoutGroup[] groupRounds = rounds
                .Where(round => round.SelectionKey == selectionGroup.Id)
                .ToArray();
            int expectedSets = index < 15 ? firstHalfSets : secondHalfSets;
            Assert.Equal(expectedSets, groupRounds.Length);
            Assert.All(groupRounds, round =>
                Assert.Equal(
                    state.SelectedExerciseIds[selectionGroup.Id],
                    service.GetSelectedExercise(state, round).Id));
        }
    }

    [Fact]
    public void ShuffleRejectsTheCurrentExerciseAndReplacesOnlyItsSlot()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        IReadOnlyList<WorkoutGroup> groups = service.GetActiveGroups(state);
        WorkoutGroup current = groups[0];
        int originalExerciseId = service.GetSelectedExercise(state, current).Id;
        Dictionary<string, int> otherSelections = groups
            .Skip(1)
            .ToDictionary(
                group => group.Id,
                group => service.GetSelectedExercise(state, group).Id,
                StringComparer.Ordinal);
        Dictionary<int, int> originalScores = exercises.ToDictionary(
            exercise => exercise.Id,
            exercise => exercise.Score);
        state.KeptExerciseRootIdsBySelectionGroupId[current.SelectionKey] =
            [originalExerciseId];
        state.SelectedExerciseIds[
            $"p{(int)WorkoutModifiers.Insect}|{current.SelectionKey}"] =
            originalExerciseId;

        Assert.True(service.CanShuffleNextExercise(state, current));
        ShuffledExerciseResult? result = service.ShuffleNextExercise(state, current);

        Assert.NotNull(result);
        Assert.Equal(originalExerciseId, result.RejectedExercise.Id);
        Assert.NotEqual(originalExerciseId, result.ReplacementExercise.Id);
        Assert.Same(
            result.ReplacementExercise,
            service.GetSelectedExercise(state, current));
        Assert.Empty(state.Outcomes);
        Assert.Equal(
            [originalExerciseId],
            result.ScoreUpdates.Select(exercise => exercise.Id));
        Assert.Equal(
            originalScores[originalExerciseId],
            result.RejectedExercise.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                originalExerciseId]);
        Assert.Contains(originalExerciseId, state.NextWorkoutExcludedExerciseIds);
        Assert.DoesNotContain(originalExerciseId, state.LastKeptExerciseIds);
        Assert.DoesNotContain(originalExerciseId, state.SelectedExerciseIds.Values);
        Assert.All(exercises.Where(exercise => exercise.Id != originalExerciseId),
            exercise => Assert.Equal(originalScores[exercise.Id], exercise.Score));
        Assert.All(groups.Skip(1), group =>
            Assert.Equal(
                otherSelections[group.Id],
                service.GetSelectedExercise(state, group).Id));
    }

    [Fact]
    public void ShuffleDoesNotOfferAnotherAliasOfTheRejectedMovement()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise current = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 100),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 50),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alternative = QualifiedForGroup(3, groups[0]);
        Exercise middle = QualifiedForGroup(4, groups[1]);
        Exercise last = QualifiedForGroup(5, groups[2]);
        var service = new ExerciseSessionService(
            [current, alias, alternative, middle, last],
            new AlwaysZeroRandom());
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup currentGroup = service.GetActiveGroups(state)[0];

        ShuffledExerciseResult? result =
            service.ShuffleNextExercise(state, currentGroup);

        Assert.NotNull(result);
        Assert.Equal(current.Id, result.RejectedExercise.Id);
        Assert.Equal(alternative.Id, result.ReplacementExercise.Id);
    }

    [Fact]
    public void ShuffleVisitsEveryEligibleAlternativeWithoutRepeating()
    {
        Exercise[] exercises =
        [
            .. ThreeGroupCatalog(),
            QualifiedExercise(8, CanonicalMuscleGroup.ScapularGirdle, 3),
        ];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)!;
        var visitedExerciseIds = new HashSet<int>
        {
            service.GetSelectedExercise(state, group).Id,
        };

        int shuffleCount = 0;
        while (service.CanShuffleNextExercise(state, group))
        {
            ShuffledExerciseResult result =
                service.ShuffleNextExercise(state, group)!;
            Assert.True(visitedExerciseIds.Add(result.ReplacementExercise.Id));
            Assert.Empty(state.Outcomes);
            Assert.True(++shuffleCount < 10);
        }

        Assert.Equal([5, 6, 8], visitedExerciseIds.Order());
        int currentExerciseId = service.GetSelectedExercise(state, group).Id;
        Assert.Equal(
            visitedExerciseIds.Where(id => id != currentExerciseId).Order(),
            state.NextWorkoutExcludedExerciseIds
                .Where(visitedExerciseIds.Contains)
                .Order());
    }

    [Fact]
    public void SequenceShuffleRejectsEveryMemberAndStopsAfterBlockOne()
    {
        const CanonicalMuscleGroup primary =
            CanonicalMuscleGroup.ForearmFlexorsAndPronators;
        Exercise third = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(3, primary, -100),
            4);
        Exercise fourth = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(4, primary, -100),
            3);
        Exercise[] exercises = [.. DirectionPairCatalog(), third, fourth];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup lead = service.GetActiveGroups(state).Single(round =>
            round.SequenceBlockIndex == 0 &&
            service.GetSelectedExercise(state, round).Id == 1);
        CompleteRoundsBefore(service, state, lead);

        Assert.True(service.CanShuffleNextExercise(state, lead));
        Exercise rejectedLead = service.GetSelectedExercise(state, lead);
        Exercise rejectedPartner = exercises.Single(exercise => exercise.Id == 2);
        int rejectedLeadScore = rejectedLead.Score;
        int rejectedPartnerScore = rejectedPartner.Score;
        ShuffledExerciseResult? result = service.ShuffleNextExercise(state, lead);

        Assert.NotNull(result);
        Assert.DoesNotContain(
            result.ReplacementExercise.Id,
            new[] { rejectedLead.Id, rejectedPartner.Id });
        Assert.Equal(
            [rejectedLead.Id, rejectedPartner.Id],
            result.ScoreUpdates.Select(exercise => exercise.Id));
        Assert.Equal(rejectedLeadScore, rejectedLead.Score);
        Assert.Equal(rejectedPartnerScore, rejectedPartner.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(lead.Order)][
                rejectedLead.Id]);
        Assert.Contains(rejectedLead.Id, state.NextWorkoutExcludedExerciseIds);
        Assert.Contains(rejectedPartner.Id, state.NextWorkoutExcludedExerciseIds);
        WorkoutGroup replacementLead = service.GetNextGroup(state)!;
        Assert.Same(
            result.ReplacementExercise,
            service.GetSelectedExercise(state, replacementLead));
        Assert.Equal(45, service.GetActiveGroups(state).Count);
        Assert.Equal(-100, third.Score);
        Assert.Equal(-100, fourth.Score);

        Exercise[] secondExercises = DirectionPairCatalog();
        var secondService = new ExerciseSessionService(
            secondExercises,
            new Random(1));
        var secondState = new WorkoutState();
        secondService.StartWorkout(secondState, 45, WorkoutModifiers.None);
        WorkoutGroup secondLead = secondService.GetActiveGroups(secondState)
            .Single(round => round.SequenceBlockIndex == 0 &&
                secondService.GetSelectedExercise(secondState, round).Id == 1);
        CompleteRoundsBefore(secondService, secondState, secondLead);
        secondService.AdvanceSequence(secondState, secondLead);
        WorkoutGroup secondBlock = secondService.GetNextGroup(secondState)!;

        Assert.Equal(secondLead.SelectionKey, secondBlock.SelectionKey);
        Assert.False(secondService.CanShuffleNextExercise(
            secondState,
            secondBlock));
        Assert.Null(secondService.ShuffleNextExercise(
            secondState,
            secondBlock));
    }

    [Fact]
    public void CrossPrimaryAtomicSequenceFillsTwoSlotsInThreeMinuteWorkout()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise first = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0], score: 100),
            2);
        Exercise second = CloneWithLinkedSequenceMember(
            QualifiedForGroup(2, groups[1], score: 100),
            1);
        Exercise firstFallback = QualifiedForGroup(10, groups[0], score: 0);
        Exercise secondFallback = QualifiedForGroup(11, groups[1], score: 0);
        Exercise third = QualifiedForGroup(12, groups[2], score: 100);
        var service = new ExerciseSessionService(
            [first, second, firstFallback, secondFallback, third],
            new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        Assert.Equal(first.Id, restored.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(first.Id, restored.SelectedExerciseIds[groups[1].Id]);
        WorkoutGroup[] rounds = service.GetActiveGroups(restored).ToArray();
        Assert.Equal(3, rounds.Length);
        Assert.Equal(
            [first.Id, second.Id, third.Id],
            rounds.Select(round => service.GetSelectedExercise(restored, round).Id));
        Assert.Equal(rounds[0].SelectionKey, rounds[1].SelectionKey);
        Assert.True(service.IsIntermediateSequenceBlock(restored, rounds[0]));
        Assert.False(service.IsIntermediateSequenceBlock(restored, rounds[1]));

        service.AdvanceSequence(restored, rounds[0]);
        long restDeadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15_000;
        service.BeginRest(restored, rounds[1], restDeadline);
        Assert.Equal(rounds[1].Id, service.GetPendingRestGroup(restored)?.Id);
        Assert.Equal(10_000, service.GetPendingRestMillisecondsRemaining(restored, restDeadline - 10_000));
        Assert.True(service.KeepPendingRest(restored));

        store.Save(restored);
        WorkoutState resumedRest = store.Load();
        service.Initialize(resumedRest);
        Assert.Equal(rounds[1].Id, service.GetPendingRestGroup(resumedRest)?.Id);
        Assert.True(resumedRest.PendingRestKept);
    }

    [Fact]
    public void IntegrationMemberKeepsRestWhenOnlySequenceTargetsSelectedMuscle()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise root = CloneWithLinkedSequenceMember(
            ExerciseWithCoverage(1, CanonicalMuscleGroup.ShoulderAbductors, 30, 1,
                score: 100, additionalSecondaries: [CanonicalMuscleGroup.RotatorCuff],
                sideSequence: ExerciseSideSequence.ScreenRightThenLeft), 2);
        Exercise member = CloneWithLinkedSequenceMember(
            ExerciseWithCoverage(2, CanonicalMuscleGroup.Chest, 30, 1,
                score: 100, additionalSecondaries: [CanonicalMuscleGroup.ShoulderAbductors]), 1);
        Exercise[] exercises = [root, member, .. groups.Select((group, index) => QualifiedForGroup(index + 100, group))];
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int> { ["r30.rotator-cuff"] = root.Id },
            KeptExerciseRootIdsBySelectionGroupId = new Dictionary<string, HashSet<int>>
            {
                ["r30.rotator-cuff"] = [root.Id],
            },
        };
        service.StartWorkout(state, 60, WorkoutModifiers.None);
        WorkoutGroup target = service.GetActiveGroups(state).Last(group =>
            service.GetSelectedExercise(state, group).Id == member.Id &&
            !group.CanonicalGroups.Contains(member.PrimaryCanonicalGroup));
        CompleteRoundsBefore(service, state, target);
        long restDeadline = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + 15_000;
        service.BeginRest(state, target, restDeadline);
        Assert.Equal(target.Id, service.GetPendingRestGroup(state)?.Id);
        Assert.Equal(10_000, service.GetPendingRestMillisecondsRemaining(state, restDeadline - 10_000));
        Assert.True(service.KeepPendingRest(state));
        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState resumed = store.Load();
        service.Initialize(resumed);
        Assert.Equal(target.Id, service.GetPendingRestGroup(resumed)?.Id);
        Assert.True(resumed.PendingRestKept);
    }

    [Fact]
    public void CrossPrimarySequenceShuffleReplacesEveryCoveredSlotAtomically()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise first = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0], score: 100),
            2);
        Exercise second = CloneWithLinkedSequenceMember(
            QualifiedForGroup(2, groups[1], score: 100),
            1);
        Exercise replacementFirst = CloneWithLinkedSequenceMember(
            QualifiedForGroup(3, groups[0], score: 0),
            4);
        Exercise replacementSecond = CloneWithLinkedSequenceMember(
            QualifiedForGroup(4, groups[1], score: 0),
            3);
        Exercise third = QualifiedForGroup(5, groups[2], score: 100);
        var service = new ExerciseSessionService(
            [first, second, replacementFirst, replacementSecond, third],
            new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup lead = service.GetActiveGroups(state)[0];

        ShuffledExerciseResult? result = service.ShuffleNextExercise(state, lead);

        Assert.NotNull(result);
        Assert.Equal(first.Id, result.RejectedExercise.Id);
        Assert.Equal(replacementFirst.Id, result.ReplacementExercise.Id);
        Assert.Equal(replacementFirst.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(replacementFirst.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(100, first.Score);
        Assert.Equal(100, second.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                first.Id]);
        Assert.Equal(
            [replacementFirst.Id, replacementSecond.Id, third.Id],
            service.GetActiveGroups(state)
                .Select(round => service.GetSelectedExercise(state, round).Id));
        WorkoutSelectionChangeLog change = Assert.Single(
            state.ActiveWorkoutSession!.SelectionChanges);
        Assert.Equal(WorkoutSelectionChangeKind.Shuffle, change.Kind);
        Assert.Equal(first.Id, change.RejectedRootExerciseId);
        Assert.Equal(replacementFirst.Id, change.ReplacementRootExerciseId);
        Assert.Equal(100, change.RejectedSelectionScoreBeforeChange);
        Assert.Equal(0, change.ReplacementSelectionScore);
    }

    [Fact]
    public void CrossPrimarySequenceUsesEachBlocksMuscularRecoveryState()
    {
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise first = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0]),
            2);
        Exercise hardSecond = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(QualifiedForGroup(2, groups[1]), 1),
            muscularDemand: 2);
        Exercise firstFallback = QualifiedForGroup(3, groups[0]);
        Exercise secondFallback = QualifiedForGroup(4, groups[1]);
        Exercise third = QualifiedForGroup(5, groups[2]);
        Exercise[] exercises =
            [first, hardSecond, firstFallback, secondFallback, third];

        var freshService = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now);
        var freshState = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        freshService.StartWorkout(freshState, 3, WorkoutModifiers.None);
        Assert.Equal(first.Id, freshState.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(first.Id, freshState.SelectedExerciseIds[groups[1].Id]);

        var recoveringService = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now);
        var recoveringState = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [hardSecond.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
        };
        recoveringService.StartWorkout(
            recoveringState,
            3,
            WorkoutModifiers.None);
        Assert.Equal(
            firstFallback.Id,
            recoveringState.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(
            secondFallback.Id,
            recoveringState.SelectedExerciseIds[groups[1].Id]);
    }

    [Fact]
    public void SequenceCanOnlyBeKeptAfterItsFinalBlock()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup lead = service.GetActiveGroups(state).Single(round =>
            round.SequenceBlockIndex == 0 &&
            service.GetSelectedExercise(state, round).Id == 1);
        WorkoutGroup decision = service.GetActiveGroups(state).Single(round =>
            round.SelectionKey == lead.SelectionKey &&
            round.IsFinalSequenceRound);
        CompleteRoundsBefore(service, state, lead);

        Assert.Throws<InvalidOperationException>(() =>
            service.RecordOutcome(state, lead, keep: true));
        Assert.DoesNotContain(lead.Id, state.Outcomes.Keys);

        service.AdvanceSequence(state, lead);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[lead.Id]);
        Assert.Equal(decision.Id, service.GetNextGroup(state)?.Id);
        RecordedWorkoutOutcome result = service.RecordOutcomeWithScoreUpdates(
            state,
            decision,
            keep: true);

        Assert.Empty(result.ScoreUpdates);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[lead.Id]);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[decision.Id]);
        Assert.Equal(100, service.GetSelectedExercise(state, lead).Score);
        Assert.Equal(100, service.GetSelectedExercise(state, decision).Score);
    }

    [Fact]
    public void PartialLegacySequenceKeepIsRemovedInsteadOfPromoted()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise root = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0]),
            1_001);
        Exercise member = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1_001, groups[0]),
            root.Id);
        Exercise[] exercises =
        [
            root,
            member,
            QualifiedForGroup(2, groups[1]),
            QualifiedForGroup(3, groups[2]),
        ];
        var service = new ExerciseSessionService(exercises, new AlwaysZeroRandom());
        var partial = new WorkoutState
        {
            Version = 21,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = root.Id,
            },
            LastKeptExerciseIds = [root.Id],
        };

        service.Initialize(partial);

        Assert.Empty(partial.LastKeptExerciseIds);

        var complete = new WorkoutState
        {
            Version = 21,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = root.Id,
            },
            LastKeptExerciseIds = [root.Id, member.Id],
        };

        service.Initialize(complete);

        Assert.Equal(
            new[] { root.Id, member.Id }.Order(),
            complete.LastKeptExerciseIds.Order());
        Assert.Contains(
            root.Id,
            complete.KeptExerciseRootIdsBySelectionGroupId[groups[0].Id]);
    }

    [Fact]
    public void IntermediateSequenceBlockHasNoKeepAndRestoresAutomaticContinuation()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] repeatedRounds = service.GetActiveGroups(state)
            .GroupBy(round => round.SelectionKey)
            .First(rounds => rounds.Count() > 1)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup firstSet = repeatedRounds[0];

        CompleteRoundsBefore(service, state, firstSet);

        service.BeginRest(state, firstSet, 123456);

        Assert.True(service.IsIntermediateSequenceBlock(state, firstSet));
        Assert.Equal(
            repeatedRounds[1].Id,
            service.GetNextSequenceBlock(state, firstSet)?.Id);
        Assert.False(service.KeepPendingRest(state));
        Assert.False(state.PendingRestKept);

        service.AdvanceSequence(state, firstSet);
        service.ClearPendingRest(state);
        WorkoutGroup nextSet = service.GetNextGroup(state)!;
        int movementDurationMilliseconds =
            MovementPhaseSchedule.TotalDurationSeconds * 1_000;
        service.PauseMovement(
            state,
            nextSet,
            movementDurationMilliseconds,
            pausedByUser: false);

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        Assert.Equal(ExerciseOutcome.Neutral, restored.Outcomes[firstSet.Id]);
        Assert.Equal(firstSet.SelectionKey, nextSet.SelectionKey);
        Assert.True(service.IsSequenceContinuationBlock(restored, nextSet));
        Assert.Equal(nextSet.Id, service.GetPendingMovementGroup(restored)?.Id);
        Assert.Equal(
            movementDurationMilliseconds,
            service.GetPendingMovementMillisecondsRemaining(restored, 999999));
        Assert.Equal(10, service.GetSelectedExercise(restored, nextSet).Score);

        service.BeginRest(restored, nextSet, 234567);
        Assert.False(service.IsIntermediateSequenceBlock(restored, nextSet));
        Assert.Null(service.GetNextSequenceBlock(restored, nextSet));
        Assert.True(service.KeepPendingRest(restored));
    }

    [Fact]
    public void PendingIntermediateRestRestoresItsUpcomingBlockWithoutAdvancing()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] repeatedRounds = service.GetActiveGroups(state)
            .GroupBy(round => round.SelectionKey)
            .First(rounds => rounds.Count() > 1)
            .OrderBy(round => round.Order)
            .ToArray();
        WorkoutGroup completedBlock = repeatedRounds[0];
        CompleteRoundsBefore(service, state, completedBlock);
        service.BeginRest(
            state,
            completedBlock,
            DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeMilliseconds());

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        WorkoutGroup pendingBlock = service.GetPendingRestGroup(restored)!;
        WorkoutGroup upcomingBlock = service.GetNextSequenceBlock(
            restored,
            pendingBlock)!;
        Assert.Equal(completedBlock.Id, pendingBlock.Id);
        Assert.Equal(repeatedRounds[1].Id, upcomingBlock.Id);
        Assert.Equal(completedBlock.Id, service.GetNextGroup(restored)?.Id);
        Assert.DoesNotContain(completedBlock.Id, restored.Outcomes.Keys);
    }

    [Fact]
    public void RepeatedSequenceDefersItsSharedKeepUntilTheFinalBlock()
    {
        Exercise[] exercises = DirectionPairCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 90, WorkoutModifiers.None);
        WorkoutGroup firstLead = service.GetActiveGroups(state)
            .First(round => round.SequenceBlockIndex == 0 &&
                round.SetCount > 1 &&
                service.GetSelectedExercise(state, round).Id == 1);
        CompleteRoundsBefore(service, state, firstLead);

        service.AdvanceSequence(state, firstLead);
        WorkoutGroup secondBlock = service.GetNextGroup(state)!;
        service.BeginRest(state, secondBlock, 123456);

        Assert.True(service.IsIntermediateSequenceBlock(state, secondBlock));
        Assert.False(service.KeepPendingRest(state));

        service.AdvanceSequence(state, secondBlock);
        service.ClearPendingRest(state);
        WorkoutGroup nextSet = service.GetNextGroup(state)!;

        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[firstLead.Id]);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[secondBlock.Id]);
        Assert.Equal(firstLead.SelectionKey, nextSet.SelectionKey);
        Assert.True(service.IsSequenceContinuationBlock(state, nextSet));
    }

    [Fact]
    public void SequenceMemberIsNeverSelectedAsAnotherBaseUnit()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise first = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.Order().First(), 10),
            2);
        Exercise second = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(2, groups[0].CanonicalGroups.Order().First(), 10),
            1);
        Exercise[] remaining = groups
            .Skip(2)
            .Select((group, index) => QualifiedForGroup(3 + index, group, 10))
            .ToArray();
        Exercise filler = FullyCoveredExercise(
            100,
            groups[1].CanonicalGroups.Order().First(),
            10);
        var service = new ExerciseSessionService(
            [first, second, filler, .. remaining],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        Assert.DoesNotContain(
            state.SelectedExerciseIds.Values,
            exerciseId => exerciseId == second.Id);
        WorkoutGroup[] sequence = service.GetActiveGroups(state)
            .Where(round => round.SelectionKey == groups[0].Id)
            .Take(2)
            .ToArray();
        Assert.Equal([first.Id, second.Id], sequence
            .Select(round => service.GetSelectedExercise(state, round).Id));
    }

    [Fact]
    public void RejectingSequenceAppliesOneSharedDecisionToEveryMember()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise partner = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1001, groups[0]),
            baseExercises[0].Id);
        baseExercises[0] = CloneWithLinkedSequenceMember(baseExercises[0], partner.Id);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] sequence = service.GetActiveGroups(state)
            .Where(round => round.SelectionKey == groups[0].Id)
            .Take(2)
            .ToArray();
        WorkoutGroup firstBlock = sequence[0];
        WorkoutGroup finalBlock = sequence[1];
        int baseExerciseId = state.SelectedExerciseIds[firstBlock.SelectionKey];
        CompleteRoundsBefore(service, state, firstBlock);
        service.AdvanceSequence(state, firstBlock);
        RecordedWorkoutOutcome result = service.RecordOutcomeWithScoreUpdates(
            state,
            finalBlock,
            keep: false);

        Assert.Equal(0, partner.Score);
        Assert.Equal(10, baseExercises.Single(exercise =>
            exercise.Id == baseExerciseId).Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                    finalBlock.Order)][baseExerciseId]);
        Assert.Equal(
            new[] { baseExerciseId, partner.Id }.Order(),
            result.ScoreUpdates.Select(exercise => exercise.Id).Order());
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[firstBlock.Id]);
        Assert.Equal(ExerciseOutcome.X, state.Outcomes[finalBlock.Id]);
        Assert.Equal(baseExerciseId, state.SelectedExerciseIds[finalBlock.SelectionKey]);
        Assert.NotEqual(baseExerciseId, partner.Id);
    }

    [Fact]
    public void VersionEightLongWorkoutRecomputesSequenceAllocationWithoutEnablingSilence()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise partner = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1001, groups[0]),
            baseExercises[0].Id);
        baseExercises[0] = CloneWithLinkedSequenceMember(baseExercises[0], partner.Id);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        state.Version = 8;
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();
        state.ActiveExtraSetSelectionGroupIds.Clear();
        state.ActiveSetCountsBySelectionGroupId.Clear();

        service.Initialize(state);

        Assert.Equal(30, state.Version);
        Assert.Equal(
            WorkoutModifiers.HardFloor |
                WorkoutModifiers.UpperBodyClothing,
            state.LastWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.ActiveWorkoutModifiers);
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        Assert.Empty(state.ActiveFullSideRoundIds);
        Assert.Equal(45, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void VersionSixteenInProgressSplitMovementMigratesIntoAtomicSequence()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(
                index + 1,
                group,
                10,
                index == 0
                    ? ExerciseSideSequence.ScreenRightThenLeft
                    : ExerciseSideSequence.Continuous))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup sequenceLead = service.GetActiveGroups(state)
            .First(round =>
                round.SelectionKey == groups[0].Id &&
                round.SetNumber == 1 &&
                round.SequenceBlockIndex == 0);
        string[] completedSelectionKeys = service.GetActiveGroups(state)
            .Where(round => round.Order < sequenceLead.Order)
            .Select(round => round.SelectionKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        state.Version = 16;
        state.Outcomes.Clear();
        foreach (string selectionKey in completedSelectionKeys)
        {
            state.Outcomes[$"{selectionKey}.set1"] = ExerciseOutcome.Tick;
        }
        state.PendingMovementGroupId = $"{sequenceLead.SelectionKey}.set1";
        state.PendingMovementMillisecondsRemaining = 10_000;
        state.PendingMovementEndsAtUnixMilliseconds = 0;
        state.PendingMovementPausedByUser = true;
        state.ActiveDirectionPartnerExerciseIds.Clear();
        state.ActiveFullSideRoundIds.Clear();

        service.Initialize(state);

        WorkoutGroup pending = service.GetPendingMovementGroup(state)!;
        Assert.Equal(30, state.Version);
        Assert.Equal(45, state.ActiveWorkoutMinutes);
        Assert.Equal(sequenceLead.SelectionKey, pending.SelectionKey);
        Assert.Equal(1, pending.SequenceBlockIndex);
        Assert.Equal(35_000, state.PendingMovementMillisecondsRemaining);
        Assert.True(state.PendingMovementPausedByUser);
        WorkoutGroup migratedLead = service.GetActiveGroups(state)
            .Single(round =>
                round.SelectionKey == sequenceLead.SelectionKey &&
                round.SetNumber == 1 &&
                round.SequenceBlockIndex == 0);
        Assert.Equal(ExerciseOutcome.Neutral, state.Outcomes[migratedLead.Id]);
        Assert.False(state.WorkoutCompleted);
    }

    [Fact]
    public void VersionSeventeenProgressMigratesOnlyWithinTheSameRepairedSequence()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise root = CloneWithLinkedSequenceMember(exercises[0], 1_001);
        Exercise member = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1_001, groups[0], 10),
            root.Id);
        exercises[0] = root;
        Exercise[] currentCatalog = [.. exercises, member];
        var service = new ExerciseSessionService(
            currentCatalog,
            new AlwaysZeroRandom());

        WorkoutState CreateLegacyState(int legacyExerciseId)
        {
            var state = new WorkoutState
            {
                Version = 17,
                CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
                ActiveWorkoutMinutes = 45,
                LastWorkoutMinutes = 45,
                SelectedExerciseIds = groups
                    .Select((group, index) => (
                        GroupId: group.Id,
                        ExerciseId: exercises[index].Id))
                    .ToDictionary(entry => entry.GroupId, entry => entry.ExerciseId),
                Outcomes = new Dictionary<string, ExerciseOutcome>
                {
                    [groups[0].Id] = ExerciseOutcome.Tick,
                },
            };
            state.SelectedExerciseIds[groups[0].Id] = legacyExerciseId;
            return state;
        }

        WorkoutState matching = CreateLegacyState(member.Id);
        service.Initialize(matching);
        WorkoutGroup[] matchingRounds = service.GetActiveGroups(matching)
            .Where(round => round.SelectionKey == groups[0].Id)
            .ToArray();
        Assert.Equal(root.Id, matching.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(
            [ExerciseOutcome.Neutral, ExerciseOutcome.Tick],
            matchingRounds.Select(round => matching.Outcomes[round.Id]));

        WorkoutState unrelated = CreateLegacyState(exercises[1].Id);
        service.Initialize(unrelated);
        WorkoutGroup[] unrelatedRounds = service.GetActiveGroups(unrelated)
            .Where(round => round.SelectionKey == groups[0].Id)
            .ToArray();
        Assert.Equal(root.Id, unrelated.SelectedExerciseIds[groups[0].Id]);
        Assert.All(unrelatedRounds, round =>
            Assert.DoesNotContain(round.Id, unrelated.Outcomes.Keys));
    }

    [Fact]
    public void FortyFiveMinuteExtraSetsPreferPreviouslyKeptExercisesThenMuscleMass()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        Exercise[] replacements = selectionGroups
            .Select((group, index) => QualifiedForGroup(1001 + index, group))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);

        WorkoutGroup[] previousRounds = service.GetActiveGroups(state).ToArray();
        foreach (WorkoutGroup round in previousRounds)
        {
            service.RecordOutcome(state, round, keep: round.Order <= 10);
        }

        int[] expectedKeptExerciseIds = previousRounds
            .Take(10)
            .Select(round => state.SelectedExerciseIds[round.SelectionKey])
            .ToArray();
        service.AcknowledgeCompletion(state);
        service.Initialize(state);
        Assert.Equal(expectedKeptExerciseIds.Order(), state.LastKeptExerciseIds.Order());

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string[] extraSetGroupIds = selectionGroups
            .Where(group => rounds.Count(round => round.SelectionKey == group.Id) == 2)
            .Select(group => group.Id)
            .ToArray();
        string[] expectedExtraSetGroupIds = selectionGroups
            .Take(10)
            .Concat(selectionGroups.TakeLast(5))
            .Select(group => group.Id)
            .ToArray();
        Assert.Equal(expectedExtraSetGroupIds, extraSetGroupIds);
        Assert.Equal(
            expectedExtraSetGroupIds.Order(),
            state.ActiveExtraSetSelectionGroupIds.Order());

        service.Initialize(state);
        Assert.Equal(expectedExtraSetGroupIds, selectionGroups
            .Where(group => service.GetActiveGroups(state)
                .Count(round => round.SelectionKey == group.Id) == 2)
            .Select(group => group.Id));
    }

    [Fact]
    public void FortyFiveMinuteExtraSetsPreferKeepsThenUnkeptHardExercises()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .Select((exercise, index) => index is >= 10 and < 25
                ? CloneWithMuscularDemand(
                    exercise,
                    WorkoutRecoveryPolicy.HardMuscularDemand)
                : exercise)
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            KeptExerciseRootIdsBySelectionGroupId = groups.Take(10)
                .Select((group, index) => (group, index))
                .ToDictionary(
                    entry => entry.group.Id,
                    entry => new HashSet<int> { exercises[entry.index].Id },
                    StringComparer.Ordinal),
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.All(groups.Take(10), group => Assert.Equal(
            2,
            service.GetActiveGroups(state)
                .Count(round => round.SelectionKey == group.Id)));
        Assert.Equal(
            5,
            groups.Skip(10).Take(15).Count(group =>
                service.GetActiveGroups(state)
                    .Count(round => round.SelectionKey == group.Id) == 2));
        Assert.All(groups.Skip(25), group => Assert.Single(
            service.GetActiveGroups(state),
            round => round.SelectionKey == group.Id));
        Assert.Equal(15, state.ActiveExtraSetSelectionGroupIds.Count);
    }

    [Theory]
    [InlineData(45, 1, 15)]
    [InlineData(60, 2, 28)]
    public void LongWorkoutExtraSetsPreferSingleBlockSequencesWithinEachSetRound(
        int minutes,
        int expectedMultiBlockSetCount,
        int expectedRepeatedSingleBlockCount)
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise first = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(1, groups[0], score: 100),
                2),
            WorkoutRecoveryPolicy.HardMuscularDemand);
        Exercise second = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(2, groups[1], score: 100),
                1),
            WorkoutRecoveryPolicy.HardMuscularDemand);
        Exercise[] singleBlockExercises = groups
            .Skip(2)
            .Select((group, index) => QualifiedForGroup(
                100 + index,
                group,
                score: 100))
            .ToArray();
        var service = new ExerciseSessionService(
            [first, second, .. singleBlockExercises],
            new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };

        service.StartWorkout(state, minutes, WorkoutModifiers.None);

        Assert.Equal(
            expectedMultiBlockSetCount,
            state.ActiveSetCountsBySelectionGroupId[groups[0].Id]);
        Assert.Equal(
            expectedRepeatedSingleBlockCount,
            groups.Skip(2).Count(group =>
                state.ActiveSetCountsBySelectionGroupId[group.Id] == 2));
        Assert.All(
            state.ActiveSetCountsBySelectionGroupId.Values,
            setCount => Assert.InRange(setCount, 1, 2));
    }

    [Fact]
    public void KeptMultiBlockSequenceRepeatsBeforeUnkeptSingleBlockExercises()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise first = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0], score: 100),
            2);
        Exercise second = CloneWithLinkedSequenceMember(
            QualifiedForGroup(2, groups[1], score: 100),
            1);
        Exercise[] singleBlockExercises = groups
            .Skip(2)
            .Select((group, index) => QualifiedForGroup(
                100 + index,
                group,
                score: 100))
            .ToArray();
        var service = new ExerciseSessionService(
            [first, second, .. singleBlockExercises],
            new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            KeptExerciseRootIdsBySelectionGroupId = new Dictionary<string, HashSet<int>>(
                StringComparer.Ordinal)
            {
                [groups[0].Id] = [first.Id],
            },
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Equal(
            2,
            state.ActiveSetCountsBySelectionGroupId[groups[0].Id]);
        Assert.Equal(
            13,
            groups.Skip(2).Count(group =>
                state.ActiveSetCountsBySelectionGroupId[group.Id] == 2));
        Assert.All(
            state.ActiveSetCountsBySelectionGroupId.Values,
            setCount => Assert.InRange(setCount, 1, 2));
    }

    [Theory]
    [InlineData(3, 5)]
    [InlineData(5, 3)]
    public void KeepsCarryAcrossWorkoutDurationResolutions(
        int previousMinutes,
        int nextMinutes)
    {
        WorkoutGroup[] previousGroups = MassGroupingTaxonomy
            .GetResolution(previousMinutes)
            .Groups
            .ToArray();
        WorkoutGroup[] nextGroups = MassGroupingTaxonomy
            .GetResolution(nextMinutes)
            .Groups
            .ToArray();
        Exercise[] keptExercises = previousGroups
            .Select((group, index) => FullyCoveredExercise(
                index + 1,
                group.CanonicalGroups.Order().First()))
            .ToArray();
        Exercise[] nextDurationAlternatives = nextGroups
            .Select((group, index) => FullyCoveredExercise(
                101 + index,
                group.CanonicalGroups.Order().First(),
                score: 10))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. keptExercises, .. nextDurationAlternatives],
            new Random(1));
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            SelectedExerciseIds = previousGroups
                .Zip(keptExercises)
                .ToDictionary(pair => pair.First.Id, pair => pair.Second.Id),
            KeptExerciseRootIdsBySelectionGroupId = previousGroups
                .Zip(keptExercises)
                .ToDictionary(
                    pair => pair.First.Id,
                    pair => new HashSet<int> { pair.Second.Id },
                    StringComparer.Ordinal),
        };

        service.Initialize(state);

        foreach ((WorkoutGroup group, Exercise alternative) in
                 nextGroups.Zip(nextDurationAlternatives))
        {
            state.SelectedExerciseIds[group.Id] = alternative.Id;
        }

        service.StartWorkout(state, nextMinutes, WorkoutModifiers.None);

        HashSet<int> keptExerciseIds = keptExercises
            .Select(exercise => exercise.Id)
            .ToHashSet();
        int[] selectedExerciseIds = nextGroups
            .Select(group => state.SelectedExerciseIds[group.Id])
            .ToArray();
        Assert.Equal(previousMinutes, state.LastKeptExerciseIds.Count);
        Assert.Equal(
            Math.Min(previousMinutes, nextMinutes),
            selectedExerciseIds.Count(keptExerciseIds.Contains));
        Assert.All(
            previousGroups.Zip(keptExercises),
            pair => Assert.Contains(
                pair.Second.Id,
                state.KeptExerciseRootIdsBySelectionGroupId[pair.First.Id]));
        Assert.All(
            selectedExerciseIds.Where(keptExerciseIds.Contains),
            selectedId => Assert.Contains(
                state.KeptExerciseRootIdsBySelectionGroupId
                    .Where(entry => entry.Key.StartsWith(
                        $"r{nextMinutes}.",
                        StringComparison.Ordinal))
                    .SelectMany(entry => entry.Value),
                rootId => rootId == selectedId));
        Assert.Equal(nextMinutes, selectedExerciseIds.Distinct().Count());
    }

    [Fact]
    public void PhaseSpecificRejectionDoesNotEraseAnExistingKeep()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        Exercise kept = exercises[0];
        var service = new ExerciseSessionService(exercises, new Random(1));
        string keptSlotId = MassGroupingTaxonomy.GetGroup(
            3,
            kept.PrimaryCanonicalGroup).Id;
        var state = new WorkoutState
        {
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [keptSlotId] = [kept.Id],
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        service.FinishInterruptedWorkout(state);

        Assert.Contains(kept.Id, state.LastKeptExerciseIds);

        service.StartWorkout(state, 3, WorkoutModifiers.None);
        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            bool keep = service.GetSelectedExercise(state, round).Id != kept.Id;
            service.RecordOutcome(state, round, keep);
        }
        service.FinishInterruptedWorkout(state);

        Assert.Contains(kept.Id, state.LastKeptExerciseIds);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                kept.Id]);
    }

    [Fact]
    public void RejectedSetClearsTheSharedCachedSelectionOnceAfterLongWorkout()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        WorkoutGroup target = selectionGroups[^1];
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise original = baseline[^1];
        Exercise replacement = QualifiedForGroup(1000, target, 5);
        var service = new ExerciseSessionService(
            [.. baseline, replacement],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] targetRounds = service.GetActiveGroups(state)
            .Where(round => round.SelectionKey == target.Id)
            .ToArray();

        CompleteRoundsBefore(service, state, targetRounds[0]);
        service.RejectCurrentSequenceWithScoreUpdates(state, targetRounds[0]);
        WorkoutExercisePhase rejectedPhase =
            WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                targetRounds[0].Order);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[rejectedPhase][original.Id]);
        Assert.Equal(
            rejectedPhase,
            Assert.Single(
                Assert.Single(state.WorkoutHistory).Decisions,
                decision => decision.RootExerciseId == original.Id &&
                    decision.Outcome == ExerciseOutcome.X).ExercisePhase);

        CompleteRemainingRounds(service, state);

        Assert.Equal(10, original.Score);
        Assert.Equal(original.Id, state.SelectedExerciseIds[target.Id]);
        service.AcknowledgeCompletion(state);
        service.Initialize(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.False(state.SelectedExerciseIds.ContainsKey(target.Id));
    }

    [Fact]
    public void DoneAtomicallyClearsRejectedCachedSelectionsBeforeRelaunch()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise[] replacements = groups
            .Select((group, index) => QualifiedForGroup(1001 + index, group, 0))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 30, WorkoutModifiers.None);
        int[] rejectedIds = service.GetActiveGroups(state)
            .Where(round => round.Order % 2 == 1)
            .Select(round => service.GetSelectedExercise(state, round).Id)
            .ToArray();

        foreach (WorkoutGroup round in service.GetActiveGroups(state))
        {
            service.RecordOutcome(state, round, keep: round.Order % 2 == 0);
        }

        service.AcknowledgeCompletion(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
        Assert.False(state.WorkoutCompleted);
        Assert.False(state.CompletionAcknowledged);
        Assert.All(rejectedIds, rejectedId =>
            Assert.DoesNotContain(rejectedId, state.SelectedExerciseIds.Values));

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);
        Assert.All(rejectedIds, rejectedId =>
            Assert.DoesNotContain(rejectedId, restored.SelectedExerciseIds.Values));
    }

    [Fact]
    public void InterruptedIntermediateSetRestDoesNotCreateAHiddenDownvote()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        WorkoutGroup target = selectionGroups[15];
        Exercise[] baseline = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise original = baseline[15];
        var service = new ExerciseSessionService(
            baseline,
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup pendingRound = service.GetActiveGroups(state)
            .First(round => round.SelectionKey == target.Id);

        foreach (WorkoutGroup round in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != pendingRound.Id))
        {
            service.RecordOutcome(state, round, keep: true);
        }
        state.PendingRestGroupId = pendingRound.Id;
        state.PendingRestEndsAtUnixMilliseconds = 123456;
        Assert.True(service.IsIntermediateSequenceBlock(state, pendingRound));

        Exercise? firstResult = service.FinishInterruptedWorkout(state);
        Exercise? repeatedResult = service.FinishInterruptedWorkout(state);

        Assert.Null(firstResult);
        Assert.Null(repeatedResult);
        Assert.Equal(10, original.Score);
        Assert.Equal(original.Id, state.SelectedExerciseIds[target.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InterruptedFinalRepeatedSetRestStillSettlesExactlyOnce()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseline = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise[] replacements = groups
            .Select((group, index) => QualifiedForGroup(1001 + index, group, 5))
            .ToArray();
        var service = new ExerciseSessionService(
            [.. baseline, .. replacements],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 90, WorkoutModifiers.None);
        WorkoutGroup pendingRound = service.GetActiveGroups(state)
            .First(round =>
                service.IsSequenceContinuationBlock(state, round) &&
                !service.IsIntermediateSequenceBlock(state, round));
        Exercise original = service.GetSelectedExercise(state, pendingRound);

        CompleteRoundsBefore(service, state, pendingRound);
        service.BeginRest(state, pendingRound, 123456);

        Exercise? firstResult = service.FinishInterruptedWorkout(state);
        Exercise? repeatedResult = service.FinishInterruptedWorkout(state);

        Assert.Same(original, firstResult);
        Assert.Null(repeatedResult);
        Assert.Equal(10, original.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[
                WorkoutExercisePhasePolicy.FromOneBasedBlockOrder(
                    pendingRound.Order)][original.Id]);
        Assert.NotEqual(
            original.Id,
            state.SelectedExerciseIds.GetValueOrDefault(pendingRound.SelectionKey));
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void PersistedPendingDirectionRestCannotRecurseThroughAllocationValidation()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] baseExercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group, 10))
            .ToArray();
        Exercise partner = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1001, groups[0]),
            baseExercises[0].Id,
            silent: false);
        baseExercises[0] = CloneWithLinkedSequenceMember(
            baseExercises[0],
            partner.Id);
        Exercise filler = FullyCoveredExercise(
            2001,
            groups[0].CanonicalGroups.Order().First(),
            10);
        var service = new ExerciseSessionService(
            [.. baseExercises, partner, filler],
            new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.Silence);
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        state.ActiveDirectionPartnerExerciseIds[groups[0].Id] = partner.Id;
        state.PendingRestGroupId = $"{groups[0].Id}.direction1";
        state.PendingRestEndsAtUnixMilliseconds = 123456;

        service.Initialize(state);

        Assert.Equal(45, state.ActiveWorkoutMinutes);
        Assert.Empty(state.ActiveDirectionPartnerExerciseIds);
        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, partner.Score);
        Assert.Null(service.FinishInterruptedWorkout(state));
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void FinalPendingRoundInLongWorkoutIsTheLastSet()
    {
        WorkoutGroup[] selectionGroups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] exercises = selectionGroups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();

        foreach (WorkoutGroup round in rounds[..^1])
        {
            Assert.False(service.IsFinalPendingGroup(state, round));
            if (service.IsIntermediateSequenceBlock(state, round))
            {
                service.AdvanceSequence(state, round);
            }
            else
            {
                service.RecordOutcome(state, round, keep: true);
            }
        }

        Assert.True(service.IsFinalPendingGroup(state, rounds[^1]));
        Assert.Equal(2, rounds
            .Count(round => round.SelectionKey == rounds[^1].SelectionKey));
    }

    [Fact]
    public void HigherScoringSecondaryAssignmentOutranksLowerScoringPrimary()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise primary = QualifiedExercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            100,
            lower.CanonicalGroups.ToArray());
        Exercise otherTorso = QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors);
        Exercise upper = QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle);
        var service = new ExerciseSessionService(
            [primary, secondary, otherTorso, upper],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(secondary.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void MuscleBalanceCanOverridePrimaryTieWhenSecondaryChoiceImprovesLineup()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise primary = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            5);
        Exercise secondary = Exercise(
            2,
            CanonicalMuscleGroup.SpinalExtensors,
            5,
            lower.CanonicalGroups.ToArray());
        var service = new ExerciseSessionService(
        [
            primary,
            secondary,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(secondary.Id, service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionUsesHighestScoreBucketAmongPrimaryCandidates()
    {
        Exercise lowerScore = QualifiedExercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            -1);
        Exercise higherScore = QualifiedExercise(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3);
        var service = new ExerciseSessionService(
        [
            lowerScore,
            higherScore,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            higherScore.PrimaryCanonicalGroup);
        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void CoverageGateRunsBeforeScoreRanking()
    {
        Exercise highScoreBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5,
            100);
        Exercise lowerScoreQualified = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            -100);
        var service = new ExerciseSessionService(
        [
            highScoreBelowThreshold,
            lowerScoreQualified,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            lowerScoreQualified.PrimaryCanonicalGroup);
        Assert.Equal(
            lowerScoreQualified.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionPrefersBroadestCoverageWithinHighestScoreBucket()
    {
        Exercise narrow = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3);
        Exercise broad = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            narrow,
            broad,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            broad.PrimaryCanonicalGroup);
        Assert.Equal(broad.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void SelectionCountsCoverageOnlyInsideOwningRolledUpGroup()
    {
        Exercise crossBucketCoverage = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3,
        [
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.AbdominalWall,
            CanonicalMuscleGroup.ScapularGirdle,
            CanonicalMuscleGroup.ElbowExtensors,
        ]);
        Exercise inBucketCoverage = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            crossBucketCoverage,
            inBucketCoverage,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            inBucketCoverage.PrimaryCanonicalGroup);
        Assert.Equal(inBucketCoverage.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void HigherScoreStillOutranksBroaderCoverage()
    {
        Exercise higherScore = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            4);
        Exercise broaderLowerScore = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        var service = new ExerciseSessionService(
        [
            higherScore,
            broaderLowerScore,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            higherScore.PrimaryCanonicalGroup);
        Assert.Equal(higherScore.Id,
            service.GetSelectedExercise(state, lower).Id);
    }

    [Fact]
    public void MuscleBalanceCanOverrideCoverageTieWhenNarrowChoiceImprovesLineup()
    {
        Exercise[] canonicalExercises = MassGroupingTaxonomy.GetResolution(20).Groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        Exercise broadForearmAndHand = Exercise(
            100,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            0,
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators,
            CanonicalMuscleGroup.IntrinsicHand);
        var service = new ExerciseSessionService(
            [.. canonicalExercises, broadForearmAndHand],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 20, WorkoutModifiers.None);

        WorkoutGroup forearmAndHand = service.GetActiveGroups(state)
            .Single(group => group.Id == "r20.forearm-hand");
        Exercise narrowForearmAndHand = canonicalExercises.Single(exercise =>
            exercise.PrimaryCanonicalGroup ==
                CanonicalMuscleGroup.ForearmFlexorsAndPronators);
        Assert.Equal(
            narrowForearmAndHand.Id,
            service.GetSelectedExercise(state, forearmAndHand).Id);
    }

    [Fact]
    public void ThirtyGroupResolutionTreatsOutOfLeafSecondariesAsNoExtraCoverage()
    {
        Exercise[] canonicalExercises = Enum.GetValues<CanonicalMuscleGroup>()
            .Select(group => Exercise(
                (int)group,
                group,
                0))
            .ToArray();
        Exercise broaderOutsideLeaf = Exercise(
            100,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            0,
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators,
            CanonicalMuscleGroup.IntrinsicHand);
        var service = new ExerciseSessionService(
            [.. canonicalExercises, broaderOutsideLeaf],
            new AlwaysZeroRandom());
        var state = new WorkoutState();

        service.StartWorkout(state, 30, WorkoutModifiers.None);

        WorkoutGroup forearmFlexors = MassGroupingTaxonomy.GetGroup(
            30,
            CanonicalMuscleGroup.ForearmFlexorsAndPronators);
        Assert.Equal(
            (int)CanonicalMuscleGroup.ForearmFlexorsAndPronators,
            service.GetSelectedExercise(state, forearmFlexors).Id);
    }

    [Fact]
    public void RejectedExerciseReplacementUsesCoverageRanking()
    {
        Exercise current = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            10);
        Exercise narrowReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            9);
        Exercise broadReplacement = ExerciseWithCoverage(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            9);
        var service = new ExerciseSessionService(
        [
            current,
            narrowReplacement,
            broadReplacement,
            QualifiedExercise(4, CanonicalMuscleGroup.SpinalExtensors, 10),
            QualifiedExercise(5, CanonicalMuscleGroup.ScapularGirdle, 10),
        ], new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            current.PrimaryCanonicalGroup);

        foreach (WorkoutGroup group in groups.Where(group => group.Id != lower.Id))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        service.RecordOutcome(state, lower, keep: false);
        service.FinishInterruptedWorkout(state);

        Assert.False(state.SelectedExerciseIds.ContainsKey(lower.Id));
        service.PrepareWorkout(state, 3, WorkoutModifiers.None);
        Assert.Equal(broadReplacement.Id, state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void SecondaryOnlyCandidateIsEligibleWhenItMeetsCoverageGate()
    {
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(3, "r3.lower-limbs");
        Exercise secondaryForLower = Exercise(
            1,
            CanonicalMuscleGroup.SpinalExtensors,
            10,
            lower.CanonicalGroups.Take(6).ToArray());
        var service = new ExerciseSessionService(
        [
            secondaryForLower,
            QualifiedExercise(2, CanonicalMuscleGroup.SpinalExtensors, 10),
            QualifiedExercise(3, CanonicalMuscleGroup.ScapularGirdle, 10),
        ],
            new Random(1));
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            secondaryForLower.Id,
            state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void ValidSavedSelectionIsNotRerankedWithoutUserRejection()
    {
        Exercise savedNarrow = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            3);
        Exercise unsavedBroad = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7,
            3);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            savedNarrow.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            savedNarrow,
            unsavedBroad,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = savedNarrow.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(savedNarrow.Id, state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void SavedSelectionBelowCoverageThresholdIsReplaced()
    {
        Exercise savedBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5,
            100);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            savedBelowThreshold.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            savedBelowThreshold,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = savedBelowThreshold.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(
            qualifyingReplacement.Id,
            state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void PendingRestFromPreviousScheduleOrderIsSettledBeforeLineupRepair()
    {
        Exercise performedBelowThreshold = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            performedBelowThreshold.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            performedBelowThreshold,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = performedBelowThreshold.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 123456,
        };

        service.Initialize(state);
        Exercise? penalized = service.FinishInterruptedWorkout(state);

        Assert.Same(performedBelowThreshold, penalized);
        Assert.Equal(0, performedBelowThreshold.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                performedBelowThreshold.Id]);
        Assert.False(state.SelectedExerciseIds.ContainsKey(lower.Id));
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        service.PrepareWorkout(state, 3, WorkoutModifiers.None);
        Assert.Equal(qualifyingReplacement.Id, state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void KeptPendingRestFromPreviousScheduleOrderRemainsKept()
    {
        Exercise performed = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        Exercise alternative = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            7);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            performed.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            performed,
            alternative,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = performed.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 123456,
            PendingRestKept = true,
        };

        service.Initialize(state);
        Exercise? penalized = service.FinishInterruptedWorkout(state);

        Assert.Null(penalized);
        Assert.Equal(0, performed.Score);
        Assert.Equal(performed.Id, state.SelectedExerciseIds[lower.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void StalePendingRestDoesNotPreserveSelectionBelowCoverageThreshold()
    {
        Exercise staleSelection = ExerciseWithCoverage(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            5);
        Exercise qualifyingReplacement = ExerciseWithCoverage(
            2,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            staleSelection.PrimaryCanonicalGroup);
        var service = new ExerciseSessionService(
        [
            staleSelection,
            qualifyingReplacement,
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors),
            QualifiedExercise(4, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [lower.Id] = staleSelection.Id,
            },
            PendingRestGroupId = lower.Id,
            PendingRestEndsAtUnixMilliseconds = 0,
        };

        service.Initialize(state);

        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(
            qualifyingReplacement.Id,
            state.SelectedExerciseIds[lower.Id]);
    }

    [Fact]
    public void RejectionDoesNotForceALowerScoreAndKeptSlotsRemainStable()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        Dictionary<string, int> initial = groups.ToDictionary(
            group => group.Id,
            group => service.GetSelectedExercise(state, group).Id);
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors);

        foreach (WorkoutGroup group in groups.Where(group => group.Id != lower.Id))
        {
            service.RecordOutcome(state, group, keep: true);
        }
        Exercise rejected = service.RecordOutcome(state, lower, keep: false);
        Exercise? pendingPenalty = service.FinishInterruptedWorkout(state);

        Assert.Null(pendingPenalty);
        Assert.Equal(10, rejected.Score);
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                rejected.Id]);
        Assert.False(state.SelectedExerciseIds.ContainsKey(lower.Id));
        Assert.All(
            groups.Where(group => group.Id != lower.Id),
            group => Assert.Equal(initial[group.Id], state.SelectedExerciseIds[group.Id]));
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.Outcomes);
        service.PrepareWorkout(state, 3, WorkoutModifiers.None);
        // One downvote changes 10 to 9 in this phase; it must not force the
        // lower-score 7 or 5 alternatives merely because the workout ended.
        Assert.Equal(rejected.Id, state.SelectedExerciseIds[lower.Id]);
        Assert.Contains(
            exercises.Single(exercise => exercise.Id == state.SelectedExerciseIds[lower.Id])
                .PrimaryCanonicalGroup,
            lower.CanonicalGroups);
        Assert.Equal(3, groups.Select(group => state.SelectedExerciseIds[group.Id]).Distinct().Count());
    }

    [Fact]
    public void InitializeRepairsDuplicateSavedSelectionsWithinActiveWorkout()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        Exercise overlapping = ExerciseWithCoverage(
            20,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            3,
            6,
            20,
        [
            CanonicalMuscleGroup.SpinalExtensors,
            CanonicalMuscleGroup.ScapularGirdle,
        ]);
        var service = new ExerciseSessionService([overlapping, .. exercises], new Random(1));
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = groups.ToDictionary(group => group.Id, _ => overlapping.Id),
        };

        service.Initialize(state);

        int[] selectedIds = groups.Select(group => state.SelectedExerciseIds[group.Id]).ToArray();
        WorkoutGroup lower = MassGroupingTaxonomy.GetGroup(
            3,
            overlapping.PrimaryCanonicalGroup);
        Assert.Equal(overlapping.Id, state.SelectedExerciseIds[lower.Id]);
        Assert.Equal(3, selectedIds.Distinct().Count());
        Assert.All(groups, group =>
        {
            Exercise selected = service.GetSelectedExercise(state, group);
            Assert.True(
                group.CanonicalGroups.Contains(selected.PrimaryCanonicalGroup) ||
                selected.SecondaryCanonicalGroups.Any(group.CanonicalGroups.Contains));
        });
    }

    [Fact]
    public void InitializeRepairsSavedAliasesAcrossDifferentMuscleSlots()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise root = CloneWithMuscularDemand(
            FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 20),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise alias = CloneWithMuscularDemand(
            FullyCoveredExercise(2, groups[0].CanonicalGroups.First(), 20),
            muscularDemand: 0,
            sessionMovementId: 1);
        Exercise[] fillers = groups
            .Select((group, index) => QualifiedForGroup(10 + index, group))
            .ToArray();
        var service = new ExerciseSessionService(
            [root, alias, .. fillers],
            new AlwaysZeroRandom());
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = root.Id,
                [groups[1].Id] = alias.Id,
                [groups[2].Id] = fillers[2].Id,
            },
        };

        service.Initialize(state);

        Exercise[] selected = groups
            .Select(group => service.GetSelectedExercise(state, group))
            .ToArray();
        Assert.Single(selected, exercise => exercise.Id is 1 or 2);
        Assert.Equal(
            selected.Length,
            selected
                .Select(WorkoutModifierPolicy.GetSessionMovementId)
                .Distinct()
                .Count());
    }

    [Fact]
    public void InitializeUsesGlobalMatchingWhenPreservingASavedExerciseWouldDeadEnd()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise shared = FullyCoveredExercise(1, groups[0].CanonicalGroups.First(), 100);
        Exercise firstOnly = QualifiedForGroup(2, groups[0]);
        Exercise lastOnly = QualifiedForGroup(3, groups[2]);
        var service = new ExerciseSessionService(
            [shared, firstOnly, lastOnly],
            new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = shared.Id,
                [groups[2].Id] = lastOnly.Id,
            },
        };

        service.Initialize(state);

        Assert.Equal(firstOnly.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(shared.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(lastOnly.Id, state.SelectedExerciseIds[groups[2].Id]);
    }

    [Fact]
    public void KeepPreferenceDoesNotMoveToAnotherCompatibleSlot()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise sharedKept = FullyCoveredExercise(
            1,
            groups[0].CanonicalGroups.First(),
            100);
        Exercise firstAlternative = QualifiedForGroup(2, groups[0], score: 100);
        Exercise middle = QualifiedForGroup(3, groups[1], score: 100);
        Exercise last = QualifiedForGroup(4, groups[2], score: 100);
        var service = new ExerciseSessionService(
            [sharedKept, firstAlternative, middle, last],
            new Random(1));
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [sharedKept.Id],
            },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = sharedKept.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(sharedKept.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(middle.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.DoesNotContain(
            sharedKept.Id,
            state.KeptExerciseRootIdsBySelectionGroupId.GetValueOrDefault(
                groups[1].Id) ?? []);
    }

    [Fact]
    public void FreshHardCandidateOutranksNonHardKeepAndMirrorPreference()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMirrorRelationship(
            CloneWithMuscularDemand(
                QualifiedForGroup(2, groups[0]),
                muscularDemand: 1),
            id: 2,
            mirrorRelationship: ExerciseMirrorRelationship.BenefitsGreatly);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hard, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [nonHardKeep.Id],
            },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = nonHardKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.Mirror);

        Assert.Equal(hard.Id, state.SelectedExerciseIds[
            $"p{(int)WorkoutModifiers.Mirror}|{groups[0].Id}"]);
        Assert.Contains(nonHardKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void CompletedLightWorkoutStartsTheNextFourDayCadence()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod test UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod test UTC+07",
            "NomadicMethod test UTC+07");
        DateTimeOffset dayFour = new(
            2026,
            8,
            29,
            8,
            0,
            0,
            TimeSpan.FromHours(7));
        List<WorkoutSessionLog> history = Enumerable.Range(1, 3)
            .Select(index => CompletedSession(
                index,
                dayFour.AddDays(index - 4)))
            .ToList();
        history.Add(new WorkoutSessionLog
        {
            SessionId = 99,
            StartedAtUnixMilliseconds = dayFour.AddDays(-1)
                .AddHours(1)
                .ToUnixTimeMilliseconds(),
            Status = WorkoutSessionStatus.Interrupted,
        });

        Assert.True(WorkoutLightDayPolicy.IsLightDayDue(
            history,
            dayFour.ToUnixTimeMilliseconds(),
            timeZone));

        history.Add(CompletedSession(4, dayFour, isLightDay: true));
        Assert.False(WorkoutLightDayPolicy.IsLightDayDue(
            history,
            dayFour.AddDays(1).ToUnixTimeMilliseconds(),
            timeZone));

        history.AddRange(Enumerable.Range(5, 3).Select(index =>
            CompletedSession(index, dayFour.AddDays(index - 4))));
        Assert.True(WorkoutLightDayPolicy.IsLightDayDue(
            history,
            dayFour.AddDays(4).ToUnixTimeMilliseconds(),
            timeZone));
    }

    [Fact]
    public void RegularWorkoutOnDueDayDoesNotSkipTheLightWorkout()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod due-day UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod due-day UTC+07",
            "NomadicMethod due-day UTC+07");
        DateTimeOffset dayFour = new(
            2026,
            8,
            29,
            8,
            0,
            0,
            TimeSpan.FromHours(7));
        List<WorkoutSessionLog> history = Enumerable.Range(1, 3)
            .Select(index => CompletedSession(
                index,
                dayFour.AddDays(index - 4)))
            .ToList();

        Assert.True(WorkoutLightDayPolicy.IsLightDayDue(
            history,
            dayFour.ToUnixTimeMilliseconds(),
            timeZone));

        history.Add(CompletedSession(4, dayFour));
        Assert.True(WorkoutLightDayPolicy.IsLightDayDue(
            history,
            dayFour.AddDays(1).ToUnixTimeMilliseconds(),
            timeZone));

        history.Add(CompletedSession(5, dayFour.AddDays(1), isLightDay: true));
        Assert.Equal(3, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayFour.AddDays(2).ToUnixTimeMilliseconds(),
            timeZone));
    }

    [Fact]
    public void VersionTwentyFourRecoversContiguousLegacyDayForTomorrowLightWorkout()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod migration UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod migration UTC+07",
            "NomadicMethod migration UTC+07");
        DateTimeOffset now = new(
            2026,
            8,
            30,
            8,
            0,
            0,
            TimeSpan.FromHours(7));
        DateTimeOffset legacyDay = now.AddDays(-3);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now.ToUniversalTime(),
            timeZone);
        var state = new WorkoutState
        {
            Version = 24,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            WorkoutHistory =
            [
                CompletedSession(1, now.AddDays(-2)),
                CompletedSession(2, now.AddDays(-1)),
            ],
            LastHardWorkUnixMillisecondsByPrimaryMuscle = new()
            {
                [CanonicalMuscleGroup.CalfDeepPosteriorLegAndPlantarFoot.ToString()] =
                    legacyDay.ToUnixTimeMilliseconds(),
                [CanonicalMuscleGroup.GlutealExtensors.ToString()] =
                    legacyDay.AddMinutes(4).ToUnixTimeMilliseconds(),
                [CanonicalMuscleGroup.MedialAndDeepKneeExtensors.ToString()] =
                    legacyDay.AddMinutes(12).ToUnixTimeMilliseconds(),
            },
        };

        service.Initialize(state);
        service.StartWorkout(
            state,
            3,
            service.GetDefaultWorkoutModifiers(state));

        Assert.Equal(30, state.Version);
        Assert.Single(state.LegacyCompletedTrainingDayUnixMilliseconds);
        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
        Assert.Equal(2, state.WorkoutHistory.Count);
    }

    [Fact]
    public void LightCountdownTracksHourLongWorkoutsAcrossTheRepeatingCadence()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod countdown UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod countdown UTC+07",
            "NomadicMethod countdown UTC+07");
        DateTimeOffset dayOne = new(
            2026,
            8,
            26,
            8,
            0,
            0,
            TimeSpan.FromHours(7));
        var history = new List<WorkoutSessionLog>();

        Assert.Equal(3, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.ToUnixTimeMilliseconds(),
            timeZone));
        history.Add(CompletedSession(1, dayOne));
        Assert.Equal(2, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddHours(1).ToUnixTimeMilliseconds(),
            timeZone));
        Assert.Equal(2, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(1).ToUnixTimeMilliseconds(),
            timeZone));
        history.Add(CompletedSession(2, dayOne.AddDays(1)));
        Assert.Equal(1, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(2).ToUnixTimeMilliseconds(),
            timeZone));
        history.Add(CompletedSession(3, dayOne.AddDays(2)));
        Assert.Equal(0, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(2).AddHours(1).ToUnixTimeMilliseconds(),
            timeZone));
        Assert.Equal(0, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(3).ToUnixTimeMilliseconds(),
            timeZone));

        history.Add(CompletedSession(4, dayOne.AddDays(3), isLightDay: true));
        Assert.Equal(0, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(3).AddHours(1).ToUnixTimeMilliseconds(),
            timeZone));
        Assert.Equal(3, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            dayOne.AddDays(4).ToUnixTimeMilliseconds(),
            timeZone));
    }

    [Fact]
    public void LightCountdownUsesLatestLightWorkoutInsteadOfLegacyStreakModulo()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod phone UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod phone UTC+07",
            "NomadicMethod phone UTC+07");
        DateTimeOffset today = new(
            2026,
            9,
            4,
            6,
            33,
            0,
            TimeSpan.FromHours(7));
        DateTimeOffset firstLoggedDay = new(
            2026,
            8,
            28,
            7,
            0,
            0,
            TimeSpan.FromHours(7));
        List<WorkoutSessionLog> history = Enumerable.Range(1, 7)
            .Select(index => CompletedSession(
                index,
                firstLoggedDay.AddDays(index - 1),
                isLightDay: index == 6))
            .ToList();
        long legacyDay = firstLoggedDay.AddDays(-1).ToUnixTimeMilliseconds();

        Assert.Equal(2, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            today.ToUnixTimeMilliseconds(),
            timeZone,
            [legacyDay]));

        history.Add(CompletedSession(8, today));
        Assert.Equal(1, WorkoutLightDayPolicy.GetWorkoutsUntilLightDay(
            history,
            60,
            today.AddHours(1).ToUnixTimeMilliseconds(),
            timeZone,
            [legacyDay]));
    }

    [Fact]
    public void ManualLightModeIsNotRememberedAsTheNextSessionDefault()
    {
        DateTimeOffset now = new(2026, 8, 26, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState();

        service.StartWorkout(
            state,
            3,
            WorkoutModifiers.Silence | WorkoutModifiers.Light);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(WorkoutModifiers.Silence, state.LastWorkoutModifiers);
        Assert.Equal(
            WorkoutModifiers.Silence,
            service.GetDefaultWorkoutModifiers(state));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutomaticLightDayCannotBeBypassedByExplicitRegularModifiers(
        bool nearlyCompleted)
    {
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups
            .Select((group, index) => QualifiedForGroup(index + 1, group))
            .ToArray();
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState
        {
            LastWorkoutModifiers = WorkoutModifiers.Insect |
                WorkoutModifiers.Light,
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index =>
                {
                    WorkoutSessionLog session = CompletedSession(
                        index, now.AddDays(index - 4));
                    if (nearlyCompleted)
                    {
                        int count = new[] { 53, 56, 55 }[index - 1];
                        session.Blocks = Enumerable.Range(1, count)
                            .Select(block => new WorkoutBlockLog
                            {
                                CompletedAtUnixMilliseconds =
                                    session.StartedAtUnixMilliseconds + block * 60_000L,
                            }).ToList();
                    }
                    return session;
                })
                .ToList(),
        };

        Assert.Equal(
            WorkoutModifiers.Insect | WorkoutModifiers.Light,
            service.GetDefaultWorkoutModifiers(state));

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(WorkoutModifiers.Light, state.ActiveWorkoutModifiers);
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
        service.ReconfigureActiveWorkout(state, WorkoutModifiers.None,
            service.GetNextGroup(state)!.Id);
        Assert.Equal(WorkoutModifiers.Light, state.ActiveWorkoutModifiers);
    }

    [Fact]
    public void AutomaticLightIsRecheckedWhenActivatingAnEarlierPreparedWorkout()
    {
        DateTimeOffset now = new(2026, 9, 3, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise[] exercises = groups.SelectMany((group, index) => new[]
        {
            CloneWithMuscularDemand(QualifiedForGroup(index + 1, group), 2),
            CloneWithMuscularDemand(QualifiedForGroup(index + 101, group), 0),
        }).ToArray();
        var service = new ExerciseSessionService(exercises,
            new AlwaysZeroRandom(), () => now, TimeZoneInfo.Utc);
        var state = new WorkoutState
        {
            WorkoutHistory = [CompletedSession(1, now.AddDays(-2)),
                CompletedSession(2, now.AddDays(-1))],
        };
        service.PrepareWorkout(state, 3, WorkoutModifiers.None);
        Assert.False(state.ActiveWorkoutIsLightDay);
        Assert.Contains(service.GetActiveGroups(state), group =>
            service.GetSelectedExercise(state, group).MuscularDemand == 2);

        state.WorkoutHistory.Add(CompletedSession(3, now));
        now = now.AddHours(2);
        service.ActivatePreparedWorkout(state);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
        Assert.All(service.GetActiveGroups(state), group =>
            Assert.Equal(0, service.GetSelectedExercise(state, group).MuscularDemand));
        Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        Assert.Equal(3, state.WorkoutHistory.Count);
    }

    [Fact]
    public void VersionTwentySevenMigratesAnActiveLightWorkoutIntoItsOwnProfile()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise lowerScoredEasy = CloneWithMuscularDemand(
            QualifiedForGroup(99, groups[0], score: -1),
            muscularDemand: 0);
        Exercise[] selected = [hard, .. groups
            .Skip(1)
            .Select((group, index) => QualifiedForGroup(index + 2, group))];
        Exercise[] exercises = [.. selected, lowerScoredEasy];
        Dictionary<string, int> selectedByGroup = groups
            .Select((group, index) => (
                GroupId: group.Id,
                ExerciseId: selected[index].Id))
            .ToDictionary(
                entry => entry.GroupId,
                entry => entry.ExerciseId,
                StringComparer.Ordinal);
        var state = new WorkoutState
        {
            Version = 27,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastWorkoutMinutes = 3,
            LastWorkoutModifiers = WorkoutModifiers.Silence |
                WorkoutModifiers.Light,
            ActiveWorkoutMinutes = 3,
            ActiveWorkoutModifiers = WorkoutModifiers.Silence,
            ActiveWorkoutIsLightDay = true,
            SelectedExerciseIds = selectedByGroup.ToDictionary(
                entry => $"p{(int)WorkoutModifiers.Silence}|{entry.Key}",
                entry => entry.Value,
                StringComparer.Ordinal),
        };
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom());

        service.Initialize(state);

        WorkoutModifiers lightProfile = WorkoutModifiers.Silence |
            WorkoutModifiers.Light;
        Assert.Equal(30, state.Version);
        Assert.Equal(WorkoutModifiers.Silence, state.LastWorkoutModifiers);
        Assert.Equal(lightProfile, state.ActiveWorkoutModifiers);
        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(
            hard.Id,
            state.SelectedExerciseIds[
                $"p{(int)WorkoutModifiers.Silence}|{groups[0].Id}"]);
        Assert.Equal(
            lowerScoredEasy.Id,
            state.SelectedExerciseIds[$"p{(int)lightProfile}|{groups[0].Id}"]);
        Assert.All(service.GetActiveGroups(state), round => Assert.Equal(
            0,
            service.GetSelectedExercise(state, round).MuscularDemand));
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
        Assert.Equal(lightProfile, state.ActiveWorkoutSession.Modifiers);
    }

    [Fact]
    public void LegacyDayInferenceRejectsSparseHardWorkEvidence()
    {
        DateTimeOffset now = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);
        IReadOnlyList<long> inferred = WorkoutLightDayPolicy
            .InferLegacyCompletedTrainingDays(
                [
                    CompletedSession(1, now.AddDays(-2)),
                    CompletedSession(2, now.AddDays(-1)),
                ],
                new Dictionary<string, long>
                {
                    [CanonicalMuscleGroup.GlutealExtensors.ToString()] =
                        now.AddDays(-3).ToUnixTimeMilliseconds(),
                    [CanonicalMuscleGroup.MedialAndDeepKneeExtensors.ToString()] =
                        now.AddDays(-3).AddMinutes(4).ToUnixTimeMilliseconds(),
                },
                [],
                now.ToUnixTimeMilliseconds(),
                TimeZoneInfo.Utc);

        Assert.Empty(inferred);
    }

    [Fact]
    public void LightDayTopBucketDemandZeroOutranksHardKeepWithoutDeletingIt()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.CreateCustomTimeZone(
            "NomadicMethod selection UTC+07",
            TimeSpan.FromHours(7),
            "NomadicMethod selection UTC+07",
            "NomadicMethod selection UTC+07");
        DateTimeOffset now = new(
            2026,
            8,
            29,
            8,
            0,
            0,
            TimeSpan.FromHours(7));
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 0);
        Exercise middle = CloneWithMuscularDemand(
            QualifiedForGroup(3, groups[1]),
            muscularDemand: 0);
        Exercise last = CloneWithMuscularDemand(
            QualifiedForGroup(4, groups[2]),
            muscularDemand: 0);
        var service = new ExerciseSessionService(
            [hardKeep, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now.ToUniversalTime(),
            timeZone);
        var state = new WorkoutState
        {
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index => CompletedSession(
                    index,
                    now.AddDays(index - 4)))
                .ToList(),
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [hardKeep.Id],
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.Light);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(
            easy.Id,
            state.SelectedExerciseIds[$"p256|{groups[0].Id}"]);
        Assert.Contains(
            hardKeep.Id,
            state.KeptExerciseRootIdsBySelectionGroupId[groups[0].Id]);
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
    }

    [Fact]
    public void LightDayPullsDemandZeroFromALowerScoreBucketWithoutChangingScores()
    {
        TimeZoneInfo timeZone = TimeZoneInfo.Utc;
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise topScoredHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 0),
            muscularDemand: 2);
        Exercise lowerScoredEasy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: -1),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [topScoredHard, lowerScoredEasy, middle, last],
            new AlwaysZeroRandom(),
            () => now,
            timeZone);
        var state = new WorkoutState
        {
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index => CompletedSession(
                    index,
                    now.AddDays(index - 4)))
                .ToList(),
        };

        service.StartWorkout(state, 3, WorkoutModifiers.Light);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(
            lowerScoredEasy.Id,
            state.SelectedExerciseIds[$"p256|{groups[0].Id}"]);
        Assert.Equal(0, topScoredHard.Score);
        Assert.Equal(-1, lowerScoredEasy.Score);
    }

    [Fact]
    public void VersionTwentyEightReplansUnfinishedActiveLightWorkWithoutRewritingCompletedWork()
    {
        DateTimeOffset now = new(2026, 9, 2, 6, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(5).Groups.ToArray();
        Exercise[] easy = groups
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(index + 1, group),
                muscularDemand: 0))
            .ToArray();
        Exercise hardRoot = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(101, groups[1]),
                102),
            muscularDemand: 2);
        Exercise hardMember = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(102, groups[2]),
                101),
            muscularDemand: 2);
        Exercise hardThird = CloneWithMuscularDemand(
            QualifiedForGroup(103, groups[3]),
            muscularDemand: 2);
        Exercise hardFourth = CloneWithMuscularDemand(
            QualifiedForGroup(104, groups[4]),
            muscularDemand: 2);
        Exercise[] exercises =
            [.. easy, hardRoot, hardMember, hardThird, hardFourth];
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState();
        service.StartWorkout(state, 5, WorkoutModifiers.Light);
        Assert.All(
            service.GetActiveGroups(state),
            round => Assert.Equal(
                0,
                service.GetSelectedExercise(state, round).MuscularDemand));
        state.CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision;
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        string profilePrefix = $"p{(int)WorkoutModifiers.Light}|";
        state.SelectedExerciseIds[$"{profilePrefix}{groups[1].Id}"] =
            hardRoot.Id;
        state.SelectedExerciseIds[$"{profilePrefix}{groups[2].Id}"] =
            hardRoot.Id;
        state.SelectedExerciseIds[$"{profilePrefix}{groups[3].Id}"] =
            hardThird.Id;
        state.SelectedExerciseIds[$"{profilePrefix}{groups[4].Id}"] =
            hardFourth.Id;
        state.ActiveSetCountsBySelectionGroupId = new()
        {
            [groups[0].Id] = 1,
            [groups[1].Id] = 1,
            [groups[3].Id] = 1,
            [groups[4].Id] = 1,
        };
        state.ActiveExtraSetSelectionGroupIds.Clear();
        state.ActiveSelectionGroupOrder.Clear();

        WorkoutGroup completed = service.GetNextGroup(state)!;
        Assert.Equal(groups[0].Id, completed.SelectionKey);
        service.RecordOutcome(state, completed, keep: true);
        state.KeptExerciseRootIdsBySelectionGroupId[groups[0].Id] =
            [easy[0].Id];
        state.LastKeptExerciseIds.Add(easy[0].Id);
        WorkoutGroup firstHardBlock = service.GetNextGroup(state)!;
        Assert.Equal(groups[1].Id, firstHardBlock.SelectionKey);
        Assert.Equal(0, firstHardBlock.SequenceBlockIndex);
        service.BeginRest(
            state,
            firstHardBlock,
            now.ToUnixTimeMilliseconds() + 15_000);
        service.AdvanceSequence(state, firstHardBlock);
        WorkoutGroup unfinishedHardBlock = service.GetNextGroup(state)!;
        Assert.Equal(groups[1].Id, unfinishedHardBlock.SelectionKey);
        Assert.Equal(1, unfinishedHardBlock.SequenceBlockIndex);
        service.PauseMovement(
            state,
            unfinishedHardBlock,
            millisecondsRemaining: 30_000,
            pausedByUser: true);
        Assert.Single(state.ActiveWorkoutSession.Blocks);
        Assert.Single(state.ActiveWorkoutSession.Decisions);
        state.Version = 28;

        var restoredService = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now.AddMinutes(1),
            TimeZoneInfo.Utc);
        restoredService.Initialize(state);

        Assert.Equal(30, state.Version);
        Assert.Equal(sessionId, state.ActiveWorkoutSession!.SessionId);
        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[completed.Id]);
        Assert.Single(state.ActiveWorkoutSession.Decisions);
        Assert.Single(state.ActiveWorkoutSession.Blocks);
        Assert.Equal(hardRoot.Id, state.ActiveWorkoutSession.Blocks[0].RootExerciseId);
        Assert.Null(state.PendingRestGroupId);
        WorkoutGroup restarted = restoredService.GetNextGroup(state)!;
        Assert.Equal(groups[1].Id, restarted.SelectionKey);
        Assert.Equal(0, restarted.SequenceBlockIndex);
        Assert.Equal(restarted.Id, state.PendingMovementGroupId);
        Assert.Equal(50_000, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(0, state.PendingMovementEndsAtUnixMilliseconds);
        Assert.True(state.PendingMovementPausedByUser);
        Assert.Equal(
            0,
            restoredService.GetSelectedExercise(state, restarted).MuscularDemand);
        Assert.All(
            restoredService.GetActiveGroups(state),
            round => Assert.Equal(
                0,
                restoredService.GetSelectedExercise(state, round).MuscularDemand));
        Assert.Contains(
            easy[0].Id,
            state.KeptExerciseRootIdsBySelectionGroupId[groups[0].Id]);
        WorkoutModifierChangeLog migration = Assert.Single(
            state.ActiveWorkoutSession.ModifierChanges);
        Assert.Equal(WorkoutModifiers.Light, migration.PreviousModifiers);
        Assert.Equal(WorkoutModifiers.Light, migration.NewModifiers);
        Assert.All(migration.PlannedSelections, selection =>
            Assert.Equal(
                0,
                exercises.Single(exercise =>
                    exercise.Id == selection.RootExerciseId).MuscularDemand));
    }

    [Fact]
    public void LightDayRequiresEveryBlockOfAnAtomicSequenceToBeDemandZero()
    {
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(5).Groups.ToArray();
        Exercise mixedRoot = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(1, groups[0]),
                2),
            muscularDemand: 0);
        Exercise moderateMember = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(2, groups[1]),
                1),
            muscularDemand: 1);
        Exercise easyFirst = CloneWithMuscularDemand(
            QualifiedForGroup(3, groups[0]),
            muscularDemand: 0);
        Exercise easySecond = CloneWithMuscularDemand(
            QualifiedForGroup(4, groups[1]),
            muscularDemand: 0);
        Exercise[] fillers = groups.Skip(2)
            .Select((group, index) => CloneWithMuscularDemand(
                QualifiedForGroup(5 + index, group),
                muscularDemand: 0))
            .ToArray();
        var service = new ExerciseSessionService(
            [mixedRoot, moderateMember, easyFirst, easySecond, .. fillers],
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState
        {
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index => CompletedSession(
                    index,
                    now.AddDays(index - 4)))
                .ToList(),
        };
        state.SelectedExerciseIds[groups[0].Id] = mixedRoot.Id;
        state.SelectedExerciseIds[groups[1].Id] = mixedRoot.Id;
        foreach ((WorkoutGroup group, Exercise filler) in groups
                     .Skip(2)
                     .Zip(fillers))
        {
            state.SelectedExerciseIds[group.Id] = filler.Id;
        }

        service.StartWorkout(state, 5, WorkoutModifiers.Light);

        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(
            easyFirst.Id,
            state.SelectedExerciseIds[$"p256|{groups[0].Id}"]);
        Assert.Equal(
            easySecond.Id,
            state.SelectedExerciseIds[$"p256|{groups[1].Id}"]);
        Assert.DoesNotContain(
            mixedRoot.Id,
            state.SelectedExerciseIds
                .Where(entry => entry.Key.StartsWith("p256|", StringComparison.Ordinal))
                .Select(entry => entry.Value));
    }

    [Fact]
    public void LightDayShuffleUsesTheBestAvailableDemandZeroExercise()
    {
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise easyOne = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 0);
        Exercise easyTwo = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 0);
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(3, groups[0]),
            muscularDemand: 2);
        Exercise middle = QualifiedForGroup(4, groups[1]);
        Exercise last = QualifiedForGroup(5, groups[2]);
        var service = new ExerciseSessionService(
            [easyOne, easyTwo, hard, middle, last],
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState
        {
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index => CompletedSession(
                    index,
                    now.AddDays(index - 4)))
                .ToList(),
        };
        service.StartWorkout(state, 3, WorkoutModifiers.Light);
        WorkoutGroup first = service.GetNextGroup(state)!;

        ShuffledExerciseResult result = service.ShuffleNextExercise(state, first)!;

        Assert.Equal(0, result.ReplacementExercise.MuscularDemand);
    }

    [Fact]
    public void VersionTwentyThreePreparedWorkoutRecognizesExistingLightDayStreak()
    {
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hard, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now,
            TimeZoneInfo.Utc);
        var state = new WorkoutState
        {
            Version = 23,
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            LastWorkoutMinutes = 3,
            ActiveWorkoutMinutes = 3,
            ActiveWorkoutModifiers = WorkoutModifiers.None,
            SelectedExerciseIds = new()
            {
                [groups[0].Id] = hard.Id,
                [groups[1].Id] = middle.Id,
                [groups[2].Id] = last.Id,
            },
            WorkoutHistory = Enumerable.Range(1, 3)
                .Select(index => CompletedSession(
                    index,
                    now.AddDays(index - 4)))
                .ToList(),
        };

        service.Initialize(state);

        Assert.Equal(30, state.Version);
        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(
            easy.Id,
            state.SelectedExerciseIds[$"p256|{groups[0].Id}"]);
        Assert.True(state.ActiveWorkoutSession!.IsLightDay);
    }

    [Fact]
    public void SameMuscleSequenceIsRankedByItsHardestPrimaryBlock()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(30).Groups.ToArray();
        Exercise root = CloneWithLinkedSequenceMember(
            QualifiedForGroup(1, groups[0]),
            1_001);
        Exercise hardMember = CloneWithMuscularDemand(
            CloneWithLinkedSequenceMember(
                QualifiedForGroup(1_001, groups[0]),
                root.Id),
            WorkoutRecoveryPolicy.HardMuscularDemand);
        Exercise nonHardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1_002, groups[0]),
            WorkoutRecoveryPolicy.ModerateMuscularDemand);
        Exercise[] fillers = groups
            .Skip(1)
            .Select((group, index) => QualifiedForGroup(index + 2, group))
            .ToArray();
        var service = new ExerciseSessionService(
            [root, hardMember, nonHardKeep, .. fillers],
            new AlwaysZeroRandom());
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 45,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [nonHardKeep.Id],
            },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = nonHardKeep.Id,
            },
        };

        service.StartWorkout(state, 45, WorkoutModifiers.None);

        Assert.Equal(root.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Contains(nonHardKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void FreshHardKeepGetsAnOpportunityDespiteLowerSavedScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: -1),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [hardKeep.Id, nonHardKeep.Id],
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(hardKeep.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void RecoveringHardKeepYieldsToNonHardKeepWithoutBeingForgotten()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 2);
        Exercise nonHardKeep = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [hardKeep, nonHardKeep, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [hardKeep.Id, nonHardKeep.Id],
            },
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [hardKeep.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = hardKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(nonHardKeep.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Contains(hardKeep.Id, state.LastKeptExerciseIds);
        Assert.Contains(nonHardKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void RecoveringModerateKeepYieldsToEasyWorkWithoutBeingForgotten()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise moderateKeep = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0]),
            muscularDemand: 1);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0]),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [moderateKeep, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 3,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [groups[0].Id] = [moderateKeep.Id],
            },
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [moderateKeep.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = moderateKeep.Id,
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(easy.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Contains(moderateKeep.Id, state.LastKeptExerciseIds);
    }

    [Fact]
    public void HardRotationNeverOverridesAHigherPersistedUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise rejectedHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: -1),
            muscularDemand: 2);
        Exercise nonHard = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [rejectedHard, nonHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState();

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(nonHard.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(-1, rejectedHard.Score);
        Assert.Equal(0, nonHard.Score);
    }

    [Fact]
    public void RecoveryRemainsSoftWhenHardExerciseHasAHigherUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise recoveringHard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 1),
            muscularDemand: 2);
        Exercise nonHard = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 1);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recoveringHard, nonHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recoveringHard.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(recoveringHard.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void ModerateRecoveryRemainsSoftWhenExerciseHasAHigherUserScore()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise recoveringModerate = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 1),
            muscularDemand: 1);
        Exercise easy = CloneWithMuscularDemand(
            QualifiedForGroup(2, groups[0], score: 0),
            muscularDemand: 0);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recoveringModerate, easy, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recoveringModerate.PrimaryCanonicalGroup.ToString()] =
                        now.AddHours(-4).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(
            recoveringModerate.Id,
            state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void EquivalentFreshHardCandidatesFavorTheLongestRestedPrimaryMuscle()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        CanonicalMuscleGroup recentlyWorked =
            CanonicalMuscleGroup.IntrinsicHand;
        CanonicalMuscleGroup longestRested =
            CanonicalMuscleGroup.ForearmExtensorsAndSupinators;
        int requiredCoverage = WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(
            groups[0]);
        CanonicalMuscleGroup[] sharedCoverage = new[]
            {
                recentlyWorked,
                longestRested,
            }
            .Concat(groups[0].CanonicalGroups
                .Where(group =>
                    group != recentlyWorked && group != longestRested)
                .Order()
                .Take(requiredCoverage - 2))
            .ToArray();
        Exercise recentHard = CloneWithMuscularDemand(
            Exercise(
                1,
                recentlyWorked,
                0,
                sharedCoverage.Where(group => group != recentlyWorked).ToArray()),
            muscularDemand: 2);
        Exercise restedHard = CloneWithMuscularDemand(
            Exercise(
                2,
                longestRested,
                0,
                sharedCoverage.Where(group => group != longestRested).ToArray()),
            muscularDemand: 2);
        Exercise middle = QualifiedForGroup(3, groups[1]);
        Exercise last = QualifiedForGroup(4, groups[2]);
        var service = new ExerciseSessionService(
            [recentHard, restedHard, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            LastHardWorkUnixMillisecondsByPrimaryMuscle =
                new Dictionary<string, long>
                {
                    [recentlyWorked.ToString()] =
                        now.AddHours(-40).ToUnixTimeMilliseconds(),
                    [longestRested.ToString()] =
                        now.AddHours(-72).ToUnixTimeMilliseconds(),
                },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(restedHard.Id, state.SelectedExerciseIds[groups[0].Id]);
    }

    [Fact]
    public void CompletingHardExerciseStartsBothRecoveryWindowsButSkippingDoesNot()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise hard = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 10),
            muscularDemand: 2);
        Exercise alternative = QualifiedForGroup(2, groups[0]);
        Exercise middle = QualifiedForGroup(3, groups[1], score: 10);
        Exercise last = QualifiedForGroup(4, groups[2], score: 10);
        var service = new ExerciseSessionService(
            [hard, alternative, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var completedState = new WorkoutState();
        service.StartWorkout(completedState, 3, WorkoutModifiers.None);
        WorkoutGroup completedGroup = service.GetActiveGroups(completedState)
            .Single(group =>
                service.GetSelectedExercise(completedState, group).Id == hard.Id);
        foreach (WorkoutGroup prior in service.GetActiveGroups(completedState)
                     .Where(group => group.Order < completedGroup.Order))
        {
            completedState.Outcomes[prior.Id] = ExerciseOutcome.Neutral;
        }

        service.BeginRest(
            completedState,
            completedGroup,
            now.AddSeconds(15).ToUnixTimeMilliseconds());
        var stateStore = new FakeWorkoutStateStore();
        stateStore.Save(completedState);
        WorkoutState restored = stateStore.Load();

        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            restored.LastHardWorkUnixMillisecondsByPrimaryMuscle[
                hard.PrimaryCanonicalGroup.ToString()]);
        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            restored.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
                hard.PrimaryCanonicalGroup.ToString()]);
        Assert.Equal(10, hard.Score);

        var skippedState = new WorkoutState();
        service.StartWorkout(skippedState, 3, WorkoutModifiers.None);
        WorkoutGroup skippedGroup = service.GetActiveGroups(skippedState)
            .Single(group =>
                service.GetSelectedExercise(skippedState, group).Id == hard.Id);
        foreach (WorkoutGroup prior in service.GetActiveGroups(skippedState)
                     .Where(group => group.Order < skippedGroup.Order))
        {
            skippedState.Outcomes[prior.Id] = ExerciseOutcome.Neutral;
        }
        service.RecordOutcome(skippedState, skippedGroup, keep: false);

        Assert.Empty(skippedState.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        Assert.Empty(skippedState.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
    }

    [Fact]
    public void CompletingModerateExerciseStartsOnlyMeaningfulRecovery()
    {
        DateTimeOffset now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise moderate = CloneWithMuscularDemand(
            QualifiedForGroup(1, groups[0], score: 10),
            muscularDemand: 1);
        Exercise alternative = QualifiedForGroup(2, groups[0]);
        Exercise middle = QualifiedForGroup(3, groups[1], score: 10);
        Exercise last = QualifiedForGroup(4, groups[2], score: 10);
        var service = new ExerciseSessionService(
            [moderate, alternative, middle, last],
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup completedGroup = service.GetActiveGroups(state)
            .Single(group => service.GetSelectedExercise(state, group).Id ==
                moderate.Id);
        foreach (WorkoutGroup prior in service.GetActiveGroups(state)
                     .Where(group => group.Order < completedGroup.Order))
        {
            state.Outcomes[prior.Id] = ExerciseOutcome.Neutral;
        }

        service.BeginRest(
            state,
            completedGroup,
            now.AddSeconds(15).ToUnixTimeMilliseconds());

        Assert.Equal(
            now.ToUnixTimeMilliseconds(),
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[
                moderate.PrimaryCanonicalGroup.ToString()]);
        Assert.Empty(state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        Assert.Equal(10, moderate.Score);
    }

    [Fact]
    public void PreparedWorkoutDoesNotStartLoggingUntilItIsActivated()
    {
        DateTimeOffset now = new(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        var service = new ExerciseSessionService(
            ThreeGroupCatalog(),
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState();

        service.PrepareWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(3, state.ActiveWorkoutMinutes);
        Assert.Null(state.ActiveWorkoutSession);
        Assert.Empty(state.WorkoutHistory);
        Assert.Equal(1, state.NextWorkoutSessionId);
        Assert.Equal(3, service.GetActiveGroups(state).Count);

        now = now.AddMinutes(2);
        service.ActivatePreparedWorkout(state);

        Assert.NotNull(state.ActiveWorkoutSession);
        Assert.Equal(now.ToUnixTimeMilliseconds(),
            state.ActiveWorkoutSession.StartedAtUnixMilliseconds);
        Assert.Equal(2, state.NextWorkoutSessionId);
        Assert.Throws<InvalidOperationException>(() =>
            service.ActivatePreparedWorkout(state));
    }

    [Fact]
    public void CompletedWorkoutArchivesExactBlocksDecisionsAndPriorKeeps()
    {
        DateTimeOffset now = new(2026, 8, 27, 1, 0, 0, TimeSpan.Zero);
        Exercise[] exercises = ThreeGroupCatalog();
        exercises[0] = CloneWithMuscularDemand(exercises[0], muscularDemand: 2);
        string keptSlotId = MassGroupingTaxonomy.GetGroup(
            3,
            exercises[0].PrimaryCanonicalGroup).Id;
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
            KeptExerciseRootIdsBySelectionGroupId = new()
            {
                [keptSlotId] = [exercises[0].Id],
            },
        };

        service.StartWorkout(state, 3, WorkoutModifiers.None);

        Assert.NotNull(state.ActiveWorkoutSession);
        Assert.Equal(3, state.ActiveWorkoutSession.InitialSelections.Count);
        Assert.Contains(
            state.ActiveWorkoutSession.InitialSelections,
            selection => selection.RootExerciseId == exercises[0].Id &&
                selection.WasKeptAtWorkoutStart);
        Assert.Equal(
            [exercises[0].Id],
            state.ActiveWorkoutSession
                .KeptExerciseRootIdsBySelectionGroupIdAtStart[keptSlotId]);

        while (service.GetNextGroup(state) is { } group)
        {
            now = now.AddMinutes(1);
            service.BeginRest(
                state,
                group,
                now.AddSeconds(15).ToUnixTimeMilliseconds());
            service.RecordOutcome(state, group, keep: true);
            service.ClearPendingRest(state);
        }

        Assert.True(state.WorkoutCompleted);
        Assert.Null(state.ActiveWorkoutSession);
        service.Initialize(state);
        Assert.Null(state.ActiveWorkoutSession);
        Assert.Single(state.WorkoutHistory);
        WorkoutSessionLog completed = Assert.Single(state.WorkoutHistory);
        Assert.Equal(WorkoutSessionStatus.Completed, completed.Status);
        Assert.Equal(3, completed.WorkoutMinutes);
        Assert.Equal(WorkoutModifiers.None, completed.Modifiers);
        Assert.False(completed.StartedBeforeLogging);
        Assert.Equal(3, completed.Blocks.Count);
        Assert.Equal(3, completed.Decisions.Count);
        Assert.Single(
            completed.Blocks,
            block => block.MuscularDemand == NomadicMethod.Models.Exercise.MaximumMuscularDemand);
        Assert.Equal(
            [1, 2, 3],
            completed.Blocks.Select(block => block.Order));
        Assert.All(completed.Blocks, block =>
        {
            Assert.Equal(1, block.SequenceBlockNumber);
            Assert.Equal(1, block.SequenceBlockCount);
            Assert.Equal(1, block.SetNumber);
            Assert.Equal(1, block.SetCount);
            Assert.True(block.CompletedAtUnixMilliseconds > 0);
        });
        Assert.All(completed.Decisions, decision =>
        {
            Assert.Equal(ExerciseOutcome.Tick, decision.Outcome);
            Assert.Equal(1, decision.CompletedBlockCount);
            Assert.Equal(1, decision.PlannedBlockCount);
        });

        service.AcknowledgeCompletion(state);
        Assert.Single(state.WorkoutHistory);
        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        Assert.Single(restored.WorkoutHistory);
        Assert.Equal(3, restored.WorkoutHistory[0].Blocks.Count);
        Assert.Equal(
            [exercises[0].Id],
            restored.WorkoutHistory[0]
                .KeptExerciseRootIdsBySelectionGroupIdAtStart[keptSlotId]);
        service.StartWorkout(restored, 3, WorkoutModifiers.None);
        Assert.NotNull(restored.ActiveWorkoutSession);
        Assert.Contains(
            restored.ActiveWorkoutSession.InitialSelections,
            selection => selection.RootExerciseId == exercises[3].Id &&
                selection.WasKeptAtWorkoutStart);
        Assert.Contains(
            exercises[3].Id,
            restored.ActiveWorkoutSession.KeptExerciseIdsAtStart);
    }

    [Fact]
    public void InterruptedWorkoutArchivesItsCompletedSubsetExactlyOnce()
    {
        DateTimeOffset now = new(2026, 8, 27, 2, 0, 0, TimeSpan.Zero);
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(
            exercises,
            new AlwaysZeroRandom(),
            () => now);
        var state = new WorkoutState
        {
            CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision,
        };
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup first = service.GetNextGroup(state)!;
        now = now.AddMinutes(1);
        service.BeginRest(
            state,
            first,
            now.AddSeconds(15).ToUnixTimeMilliseconds());
        service.RecordOutcome(state, first, keep: false);
        service.ClearPendingRest(state);

        service.FinishInterruptedWorkout(state);
        service.FinishInterruptedWorkout(state);

        WorkoutSessionLog interrupted = Assert.Single(state.WorkoutHistory);
        Assert.Equal(WorkoutSessionStatus.Interrupted, interrupted.Status);
        Assert.Single(interrupted.Blocks);
        WorkoutDecisionLog decision = Assert.Single(interrupted.Decisions);
        Assert.Equal(ExerciseOutcome.X, decision.Outcome);
        Assert.Equal(first.SelectionKey, decision.SelectionGroupId);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Null(state.ActiveWorkoutSession);
    }

    [Fact]
    public void PreparingRejectedReplacementsUsesGlobalMatchingInsteadOfGreedyOrder()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy.GetResolution(3).Groups.ToArray();
        Exercise currentFirst = QualifiedForGroup(1, groups[0], 0);
        Exercise currentMiddle = QualifiedForGroup(2, groups[1], 0);
        Exercise currentLast = QualifiedForGroup(3, groups[2], 10);
        Exercise sharedReplacement = FullyCoveredExercise(
            4,
            groups[0].CanonicalGroups.First(),
            100);
        Exercise firstOnlyReplacement = QualifiedForGroup(5, groups[0], 5);
        var service = new ExerciseSessionService(
            [
                currentFirst,
                currentMiddle,
                currentLast,
                sharedReplacement,
                firstOnlyReplacement,
            ],
            new Random(1));
        var state = new WorkoutState
        {
            ActiveWorkoutMinutes = 3,
            SelectedExerciseIds = new Dictionary<string, int>
            {
                [groups[0].Id] = currentFirst.Id,
                [groups[1].Id] = currentMiddle.Id,
                [groups[2].Id] = currentLast.Id,
            },
        };
        service.Initialize(state);

        service.RecordOutcome(state, groups[0], keep: false);
        service.RecordOutcome(state, groups[1], keep: false);
        service.RecordOutcome(state, groups[2], keep: true);
        service.AcknowledgeCompletion(state);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.False(state.SelectedExerciseIds.ContainsKey(groups[0].Id));
        Assert.False(state.SelectedExerciseIds.ContainsKey(groups[1].Id));
        service.PrepareWorkout(state, 3, WorkoutModifiers.None);

        Assert.Equal(firstOnlyReplacement.Id, state.SelectedExerciseIds[groups[0].Id]);
        Assert.Equal(sharedReplacement.Id, state.SelectedExerciseIds[groups[1].Id]);
        Assert.Equal(currentLast.Id, state.SelectedExerciseIds[groups[2].Id]);
    }

    [Fact]
    public void AbruptClosePenalizesPendingRestExactlyOnceAndRespectsCompletedOutcomes()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var stateStore = new FakeWorkoutStateStore();
        using var database = new FakeExerciseDatabase(exercises);
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup[] groups = service.GetActiveGroups(state).ToArray();
        int[] initial = groups.Select(group => state.SelectedExerciseIds[group.Id]).ToArray();

        Exercise completedRejection = service.RecordOutcome(state, groups[0], keep: false);
        database.UpdateScore(completedRejection);
        service.RecordOutcome(state, groups[1], keep: true);
        state.PendingRestGroupId = groups[2].Id;
        state.PendingRestEndsAtUnixMilliseconds = 123456;
        state.PendingRestKept = false;
        stateStore.Save(state);

        WorkoutState restored = stateStore.Load();
        Assert.Equal(ExerciseOutcome.X, restored.Outcomes[groups[0].Id]);
        Exercise? pendingPenalty = service.FinishInterruptedWorkout(restored);
        Assert.NotNull(pendingPenalty);
        database.UpdateScore(pendingPenalty);
        stateStore.Save(restored);

        Assert.Equal(initial[2], pendingPenalty.Id);
        Assert.Equal(10, completedRejection.Score);
        Assert.Equal(10, pendingPenalty.Score);
        Assert.Equal(
            -1,
            restored.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                completedRejection.Id]);
        Assert.Equal(
            -1,
            restored.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                pendingPenalty.Id]);
        Assert.False(restored.SelectedExerciseIds.ContainsKey(groups[0].Id));
        Assert.Equal(initial[1], restored.SelectedExerciseIds[groups[1].Id]);
        Assert.False(restored.SelectedExerciseIds.ContainsKey(groups[2].Id));
        Assert.Equal(0, restored.ActiveWorkoutMinutes);
        Assert.Empty(restored.Outcomes);

        Exercise? repeated = service.FinishInterruptedWorkout(stateStore.Load());
        Assert.Null(repeated);
        Assert.Equal(10, completedRejection.Score);
        Assert.Equal(10, pendingPenalty.Score);
        Assert.Equal(2, database.Updates.Count);
    }

    [Fact]
    public void InProgressMovementSurvivesPersistenceWithExactPausedTime()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var stateStore = new FakeWorkoutStateStore();
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = new DateTimeOffset(
            2026,
            8,
            23,
            2,
            0,
            0,
            TimeSpan.Zero).ToUnixTimeMilliseconds();

        service.BeginMovement(
            state,
            group,
            millisecondsRemaining: 42_000,
            endsAtUnixMilliseconds: now + 42_000);
        stateStore.Save(state);

        WorkoutState restoredRunning = stateStore.Load();
        service.Initialize(restoredRunning);
        Assert.Equal(3, restoredRunning.ActiveWorkoutMinutes);
        Assert.Equal(
            group.Id,
            service.GetPendingMovementGroup(restoredRunning)?.Id);
        Assert.Equal(
            40_000,
            service.GetPendingMovementMillisecondsRemaining(
                restoredRunning,
                now + 2_000));
        Assert.Equal(
            42_000,
            service.GetPendingMovementMillisecondsRemaining(
                restoredRunning,
                now + (long)TimeSpan.FromMinutes(2).TotalMilliseconds));

        service.PauseMovement(
            restoredRunning,
            group,
            millisecondsRemaining: 31_123,
            pausedByUser: false);
        stateStore.Save(restoredRunning);

        WorkoutState restoredPaused = stateStore.Load();
        service.Initialize(restoredPaused);
        Assert.Equal(3, restoredPaused.ActiveWorkoutMinutes);
        Assert.Equal(group.Id, restoredPaused.PendingMovementGroupId);
        Assert.Equal(31_123, restoredPaused.PendingMovementMillisecondsRemaining);
        Assert.Equal(0, restoredPaused.PendingMovementEndsAtUnixMilliseconds);
        Assert.False(restoredPaused.PendingMovementPausedByUser);
        Assert.Equal(
            31_123,
            service.GetPendingMovementMillisecondsRemaining(
                restoredPaused,
                now + (long)TimeSpan.FromHours(2).TotalMilliseconds));
    }

    [Fact]
    public void PendingRestSurvivesInitializationAndIdentifiesTheNextRound()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long restEndsAt = DateTimeOffset.UtcNow.AddSeconds(15)
            .ToUnixTimeMilliseconds();
        service.BeginRest(state, group, restEndsAt);

        service.Initialize(state);

        Assert.Equal(group.Id, service.GetPendingRestGroup(state)?.Id);
        Assert.Equal(restEndsAt, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(3, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void UserPausedRestFreezesAndSurvivesPersistenceUntilResumed()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        service.BeginRest(state, group, now + 15_000);

        service.PauseRest(state, group, 9_876);

        Assert.True(state.PendingRestPausedByUser);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(9_876, state.PendingRestMillisecondsRemaining);
        Assert.Equal(
            9_876,
            service.GetPendingRestMillisecondsRemaining(
                state,
                now + (long)TimeSpan.FromHours(4).TotalMilliseconds));

        var store = new FakeWorkoutStateStore();
        store.Save(state);
        WorkoutState restored = store.Load();
        service.Initialize(restored);

        Assert.Equal(group.Id, service.GetPendingRestGroup(restored)?.Id);
        Assert.True(restored.PendingRestPausedByUser);
        Assert.Equal(
            9_876,
            service.GetPendingRestMillisecondsRemaining(
                restored,
                now + (long)TimeSpan.FromDays(1).TotalMilliseconds));

        long resumedAt = now + (long)TimeSpan.FromDays(1).TotalMilliseconds;
        service.ResumeRest(restored, group, resumedAt + 9_876);

        Assert.False(restored.PendingRestPausedByUser);
        Assert.Equal(0, restored.PendingRestMillisecondsRemaining);
        Assert.Equal(resumedAt + 9_876, restored.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(
            7_876,
            service.GetPendingRestMillisecondsRemaining(restored, resumedAt + 2_000));
    }

    [Fact]
    public void ClearingPausedRestClearsEveryPauseCheckpointField()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        service.BeginRest(state, group, now + 15_000);
        service.PauseRest(state, group, 8_000);

        service.ClearPendingRest(state);

        Assert.Null(state.PendingRestGroupId);
        Assert.Equal(0, state.PendingRestEndsAtUnixMilliseconds);
        Assert.Equal(0, state.PendingRestMillisecondsRemaining);
        Assert.False(state.PendingRestPausedByUser);
        Assert.False(state.PendingRestKept);
    }

    [Fact]
    public void CompletingMovementClearsResumeCheckpointBeforeRest()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup group = service.GetNextGroup(state)
            ?? throw new InvalidOperationException("Workout has no first group.");
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        service.BeginMovement(state, group, 50_000, now + 50_000);

        service.BeginRest(state, group, now + 15_000);

        Assert.Null(state.PendingMovementGroupId);
        Assert.Equal(0, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(0, state.PendingMovementEndsAtUnixMilliseconds);
        Assert.False(state.PendingMovementPausedByUser);
        Assert.Equal(group.Id, state.PendingRestGroupId);
    }

    [Fact]
    public void PendingScoreJournalRecoversExactValueOnlyOnceWithFakePersistence()
    {
        Exercise exercise = Exercise(
            1,
            CanonicalMuscleGroup.MedialAndDeepKneeExtensors,
            0);
        using var database = new FakeExerciseDatabase([exercise]);
        var store = new FakeWorkoutStateStore();
        var state = new WorkoutState();
        exercise.Score = -1;

        ScoreJournalProtocol.Stage(state, exercise, store);
        Assert.Equal(0, database.PersistedScore(exercise.Id));

        WorkoutState afterCrash = store.Load();
        ScoreJournalProtocol.Recover(afterCrash, store, database);
        Assert.Equal(-1, database.PersistedScore(exercise.Id));
        Assert.Equal((exercise.Id, -1), Assert.Single(database.Updates));
        Assert.Equal(0, store.Load().PendingScoreExerciseId);

        ScoreJournalProtocol.Recover(store.Load(), store, database);
        Assert.Single(database.Updates);
    }

    [Fact]
    public void InitializeMigratesLegacyKeptSelectionAcrossEveryResolution()
    {
        Exercise selected = FullyCoveredExercise(
            50,
            CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService([selected], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            LastWorkoutMinutes = 4,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = selected.Name,
            },
        };

        service.Initialize(state);

        Assert.Equal(5, state.LastWorkoutMinutes);
        Assert.Equal(30, state.Version);
        foreach (int minutes in MassGroupingTaxonomy.SupportedMinutes)
        {
            WorkoutGroup group = MassGroupingTaxonomy.GetGroup(
                minutes,
                selected.PrimaryCanonicalGroup);
            Assert.Equal(selected.Id, state.SelectedExerciseIds[group.Id]);
        }
        Assert.Empty(state.LegacySelectedExerciseNames);
    }

    [Fact]
    public void InitializeDoesNotSeedRejectedOrPendingLegacySelections()
    {
        Exercise rejected = Exercise(50, CanonicalMuscleGroup.GlutealExtensors);
        Exercise pending = Exercise(51, CanonicalMuscleGroup.AbdominalWall);
        var service = new ExerciseSessionService([rejected, pending], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = rejected.Name,
                ["Core"] = pending.Name,
            },
            LegacyOutcomes = new Dictionary<string, ExerciseOutcome>
            {
                ["Glutes"] = ExerciseOutcome.X,
            },
            LegacyPendingRestGroup = "Core",
            PendingRestKept = false,
        };

        service.Initialize(state);

        Assert.DoesNotContain(rejected.Id, state.SelectedExerciseIds.Values);
        Assert.DoesNotContain(pending.Id, state.SelectedExerciseIds.Values);
        Assert.Empty(state.LegacySelectedExerciseNames);
        Assert.Empty(state.LegacyOutcomes);
        Assert.Null(state.LegacyPendingRestGroup);
    }

    [Fact]
    public void LegacyPendingRestPenaltyIsReturnedOnlyOnce()
    {
        Exercise exercise = Exercise(50, CanonicalMuscleGroup.AbdominalWall);
        var service = new ExerciseSessionService([exercise], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            ActiveWorkoutMinutes = 10,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Core"] = exercise.Name,
            },
            LegacyPendingRestGroup = "Core",
            PendingRestKept = false,
            PendingRestEndsAtUnixMilliseconds = 123456,
        };

        service.Initialize(state);
        Exercise? penalty = service.FinishInterruptedWorkout(state);
        Exercise? repeated = service.FinishInterruptedWorkout(state);

        Assert.Same(exercise, penalty);
        Assert.Null(repeated);
        Assert.Equal(0, exercise.Score);
        string selectionGroupId = MassGroupingTaxonomy.GetGroup(
            10,
            exercise.PrimaryCanonicalGroup).Id;
        Assert.Equal(
            -1,
            state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][
                exercise.Id]);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Empty(state.LegacySelectedExerciseNames);
        Assert.Null(state.LegacyPendingRestGroup);
    }

    [Fact]
    public void AcknowledgedLegacyCompletionReturnsToDurationSelection()
    {
        Exercise selected = FullyCoveredExercise(
            50,
            CanonicalMuscleGroup.GlutealExtensors);
        var service = new ExerciseSessionService(
        [
            selected,
            FullyCoveredExercise(51, CanonicalMuscleGroup.SpinalExtensors),
            FullyCoveredExercise(52, CanonicalMuscleGroup.ScapularGirdle),
        ], new Random(1));
        var state = new WorkoutState
        {
            Version = 4,
            ActiveWorkoutMinutes = 10,
            WorkoutCompleted = true,
            CompletionAcknowledged = true,
            LegacySelectedExerciseNames = new Dictionary<string, string>
            {
                ["Glutes"] = selected.Name,
            },
        };

        service.Initialize(state);

        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.False(state.WorkoutCompleted);
        Assert.False(state.CompletionAcknowledged);
        Assert.Empty(state.LegacySelectedExerciseNames);
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        Assert.Equal(3, state.ActiveWorkoutMinutes);
    }

    [Fact]
    public void InitializePreservesAlreadyCompletedCurrentOutcome()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup first = service.GetActiveGroups(state)[0];
        service.RecordOutcome(state, first, keep: true);

        service.Initialize(state);

        Assert.Equal(ExerciseOutcome.Tick, state.Outcomes[first.Id]);
        Assert.Equal(service.GetActiveGroups(state)[1].Id, service.GetNextGroup(state)?.Id);
    }

    [Fact]
    public void FinalPendingGroupIsIdentifiedOnlyAfterEarlierRoundsFinish()
    {
        Exercise[] exercises = ThreeGroupCatalog();
        var service = new ExerciseSessionService(exercises, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3, WorkoutModifiers.None);
        IReadOnlyList<WorkoutGroup> groups = service.GetActiveGroups(state);

        Assert.False(service.IsFinalPendingGroup(state, groups[0]));
        service.RecordOutcome(state, groups[0], keep: true);
        Assert.False(service.IsFinalPendingGroup(state, groups[1]));
        service.RecordOutcome(state, groups[1], keep: true);

        Assert.True(service.IsFinalPendingGroup(state, groups[2]));
        Assert.False(service.IsFinalPendingGroup(state, groups[1]));
    }

    private static Exercise[] ThreeGroupCatalog(
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
    {
        return
        [
            QualifiedExercise(1, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 10, insectCompatibility),
            QualifiedExercise(2, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 5, insectCompatibility),
            QualifiedExercise(7, CanonicalMuscleGroup.MedialAndDeepKneeExtensors, 7, insectCompatibility),
            QualifiedExercise(3, CanonicalMuscleGroup.SpinalExtensors, 10, insectCompatibility),
            QualifiedExercise(4, CanonicalMuscleGroup.SpinalExtensors, 5, insectCompatibility),
            QualifiedExercise(5, CanonicalMuscleGroup.ScapularGirdle, 10, insectCompatibility),
            QualifiedExercise(6, CanonicalMuscleGroup.ScapularGirdle, 5, insectCompatibility),
        ];
    }

    private static Exercise[] ReviewedInsectCatalog()
    {
        var exercises = new List<Exercise>();
        int exerciseId = 10_000;
        foreach (CanonicalMuscleGroup primary in
                 Enum.GetValues<CanonicalMuscleGroup>())
        {
            CanonicalMuscleGroup[] secondary =
                Enum.GetValues<CanonicalMuscleGroup>()
                    .Where(group => group != primary)
                    .ToArray();
            exercises.Add(Exercise(
                exerciseId++,
                primary,
                0,
                ExerciseSideSequence.Continuous,
                ExerciseInsectCompatibility.Compatible,
                secondary));

            exercises.Add(Exercise(
                exerciseId++,
                primary,
                100,
                ExerciseSideSequence.Continuous,
                ExerciseInsectCompatibility.Incompatible,
                secondary));
        }

        return exercises.ToArray();
    }

    private static (
        WorkoutGroup[] Groups,
        Exercise[] Selected,
        WorkoutState State) CreateThirtyMinuteBalanceFixture()
    {
        WorkoutGroup[] groups = MassGroupingTaxonomy
            .GetResolution(30)
            .Groups
            .ToArray();
        Exercise[] selected = groups
            .Select((group, index) => CloneWithMuscularDemand(
                Exercise(index + 1, group.CanonicalGroups.Single()),
                NomadicMethod.Models.Exercise.MinimumMuscularDemand))
            .ToArray();
        var state = new WorkoutState
        {
            LastWorkoutMinutes = 30,
            SelectedExerciseIds = groups
                .Select((group, index) => (
                    GroupId: group.Id,
                    ExerciseId: selected[index].Id))
                .ToDictionary(
                    entry => entry.GroupId,
                    entry => entry.ExerciseId,
                    StringComparer.Ordinal),
        };
        return (groups, selected, state);
    }

    private static Exercise[] DirectionPairCatalog()
    {
        Exercise first = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(
                1,
                CanonicalMuscleGroup.ForearmFlexorsAndPronators,
                100),
            2);
        Exercise second = CloneWithLinkedSequenceMember(
            FullyCoveredExercise(
                2,
                CanonicalMuscleGroup.ForearmFlexorsAndPronators,
                100),
            1);
        CanonicalMuscleGroup[] muscleGroups =
            Enum.GetValues<CanonicalMuscleGroup>();
        Exercise[] fillers = Enumerable.Range(0, 30)
            .Select(index => FullyCoveredExercise(
                100 + index,
                muscleGroups[index % muscleGroups.Length]))
            .ToArray();
        return [first, second, .. fillers];
    }

    private static Exercise QualifiedForGroup(
        int id,
        WorkoutGroup group,
        int score = 0,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed,
        ExerciseDirectionSequence directionSequence = ExerciseDirectionSequence.None)
    {
        CanonicalMuscleGroup primary = group.CanonicalGroups
            .Order()
            .First();
        int minutes = MassGroupingTaxonomy.SupportedMinutes.Single(minutes =>
            MassGroupingTaxonomy.GetResolution(minutes).Groups.Contains(group));
        return ExerciseWithCoverage(
            id,
            primary,
            minutes,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group),
            score,
            sideSequence: sideSequence,
            insectCompatibility: insectCompatibility,
            directionSequence: directionSequence);
    }

    private static Exercise QualifiedExercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(3, primary);
        return ExerciseWithCoverage(
            id,
            primary,
            3,
            WorkoutCoveragePolicy.GetRequiredCanonicalCoverage(group),
            score,
            insectCompatibility: insectCompatibility);
    }

    private static Exercise ExerciseWithCoverage(
        int id,
        CanonicalMuscleGroup primary,
        int minutes,
        int inBucketCoverage,
        int score = 0,
        CanonicalMuscleGroup[]? additionalSecondaries = null,
        ExerciseSideSequence sideSequence = ExerciseSideSequence.Continuous,
        ExerciseInsectCompatibility insectCompatibility =
            ExerciseInsectCompatibility.Unreviewed,
        ExerciseDirectionSequence directionSequence = ExerciseDirectionSequence.None)
    {
        WorkoutGroup group = MassGroupingTaxonomy.GetGroup(minutes, primary);
        if (inBucketCoverage is < 1 || inBucketCoverage > group.CanonicalGroups.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(inBucketCoverage));
        }

        CanonicalMuscleGroup[] secondary = group.CanonicalGroups
            .Where(candidate => candidate != primary)
            .Order()
            .Take(inBucketCoverage - 1)
            .Concat(additionalSecondaries ?? [])
            .Where(candidate => candidate != primary)
            .Distinct()
            .ToArray();
        return Exercise(
            id,
            primary,
            score,
            sideSequence,
            insectCompatibility,
            directionSequence,
            secondary);
    }

    private static Exercise FullyCoveredExercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0)
    {
        return Exercise(
            id,
            primary,
            score,
            Enum.GetValues<CanonicalMuscleGroup>()
                .Where(group => group != primary)
                .ToArray());
    }

    private static WorkoutSessionLog CompletedSession(
        long sessionId,
        DateTimeOffset startedAt,
        bool isLightDay = false)
    {
        return new WorkoutSessionLog
        {
            SessionId = sessionId,
            StartedAtUnixMilliseconds = startedAt.ToUnixTimeMilliseconds(),
            EndedAtUnixMilliseconds = startedAt.AddMinutes(60)
                .ToUnixTimeMilliseconds(),
            WorkoutMinutes = 60,
            Modifiers = isLightDay
                ? WorkoutModifiers.Light
                : WorkoutModifiers.None,
            IsLightDay = isLightDay,
            Status = WorkoutSessionStatus.Completed,
        };
    }

    private static Exercise CloneWithLinkedSequenceMember(
        Exercise source,
        int linkedMemberExerciseId,
        bool? silent = null)
    {
        return new Exercise
        {
            Id = source.Id,
            Name = source.Name,
            RetiredName = source.RetiredName,
            Video = source.Video,
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            SequenceBlocks = source.Id < linkedMemberExerciseId
                ? BuildLinkedSequenceBlocks(
                    source.Id,
                    linkedMemberExerciseId,
                    source.SideSequence)
                : [],
            SessionMovementId = source.SessionMovementId,
            InsectCompatibility = source.InsectCompatibility,
            HardFloorCompatibility = source.HardFloorCompatibility,
            UpperBodyClothingRequirement =
                source.UpperBodyClothingRequirement,
            ShyCompatibility = source.ShyCompatibility,
            MirrorRelationship = source.MirrorRelationship,
            MinimumMirrorCoverage = source.MinimumMirrorCoverage,
            WallRequired = source.WallRequired,
            SoleWallContactRequired = source.SoleWallContactRequired,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = silent ?? source.Silent,
        };
    }

    private static Exercise CloneWithMuscularDemand(
        Exercise source,
        int muscularDemand,
        int? sessionMovementId = null)
    {
        return new Exercise
        {
            Id = source.Id,
            Name = source.Name,
            RetiredName = source.RetiredName,
            Video = source.Video,
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            SequenceBlocks = source.SequenceBlocks,
            SessionMovementId = sessionMovementId ?? source.SessionMovementId,
            InsectCompatibility = source.InsectCompatibility,
            HardFloorCompatibility = source.HardFloorCompatibility,
            UpperBodyClothingRequirement =
                source.UpperBodyClothingRequirement,
            ShyCompatibility = source.ShyCompatibility,
            MirrorRelationship = source.MirrorRelationship,
            MinimumMirrorCoverage = source.MinimumMirrorCoverage,
            WallRequired = source.WallRequired,
            SoleWallContactRequired = source.SoleWallContactRequired,
            MuscularDemand = muscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = source.Silent,
        };
    }

    private static Exercise CloneWithMirrorRelationship(
        Exercise source,
        int id,
        ExerciseMirrorRelationship mirrorRelationship)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            RetiredName = source.RetiredName,
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            SequenceBlocks = source.SequenceBlocks.Length == 0
                ? []
                : source.SequenceBlocks
                    .Select(block => block.ExerciseId == source.Id
                        ? block with { ExerciseId = id }
                        : block)
                    .ToArray(),
            InsectCompatibility = source.InsectCompatibility,
            HardFloorCompatibility = source.HardFloorCompatibility,
            UpperBodyClothingRequirement =
                source.UpperBodyClothingRequirement,
            ShyCompatibility = source.ShyCompatibility,
            MirrorRelationship = mirrorRelationship,
            MinimumMirrorCoverage = mirrorRelationship is
                ExerciseMirrorRelationship.MirrorOnly or
                ExerciseMirrorRelationship.BenefitsGreatly
                ? ExerciseMirrorCoverage.UpperBody
                : ExerciseMirrorCoverage.None,
            WallRequired = source.WallRequired,
            SoleWallContactRequired = source.SoleWallContactRequired,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = mirrorRelationship == ExerciseMirrorRelationship.MirrorOnly
                ? "Mirror"
                : "None",
            Silent = source.Silent,
        };
    }

    private static Exercise CloneWithWallRequirement(
        Exercise source,
        int id,
        bool wallRequired)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            RetiredName = source.RetiredName,
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = source.PrimaryCanonicalGroup,
            SecondaryCanonicalGroups = source.SecondaryCanonicalGroups,
            Practice = source.Practice,
            MotionProfile = source.MotionProfile,
            Mode = source.Mode,
            Presentation = source.Presentation,
            HoldFramePercent = source.HoldFramePercent,
            SideSequence = source.SideSequence,
            DirectionSequence = source.DirectionSequence,
            SequenceBlocks = source.SequenceBlocks.Length == 0
                ? []
                : source.SequenceBlocks
                    .Select(block => block.ExerciseId == source.Id
                        ? block with { ExerciseId = id }
                        : block)
                    .ToArray(),
            SessionMovementId = source.SessionMovementId,
            InsectCompatibility = source.InsectCompatibility,
            HardFloorCompatibility = source.HardFloorCompatibility,
            UpperBodyClothingRequirement =
                source.UpperBodyClothingRequirement,
            ShyCompatibility = source.ShyCompatibility,
            MirrorRelationship = source.MirrorRelationship,
            MinimumMirrorCoverage = source.MinimumMirrorCoverage,
            WallRequired = wallRequired,
            SoleWallContactRequired =
                wallRequired && source.SoleWallContactRequired,
            MuscularDemand = source.MuscularDemand,
            Score = source.Score,
            OnlyFeetTouchGround = source.OnlyFeetTouchGround,
            ShoeAgnostic = source.ShoeAgnostic,
            MaxSpaceMeters = source.MaxSpaceMeters,
            Equipment = source.Equipment,
            Silent = source.Silent,
        };
    }

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score = 0,
        params CanonicalMuscleGroup[] secondary)
        => Exercise(id, primary, score, ExerciseSideSequence.Continuous, secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        params CanonicalMuscleGroup[] secondary)
        => Exercise(
            id,
            primary,
            score,
            sideSequence,
            ExerciseInsectCompatibility.Unreviewed,
            secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        ExerciseInsectCompatibility insectCompatibility,
        params CanonicalMuscleGroup[] secondary)
        => Exercise(
            id,
            primary,
            score,
            sideSequence,
            insectCompatibility,
            ExerciseDirectionSequence.None,
            secondary);

    private static Exercise Exercise(
        int id,
        CanonicalMuscleGroup primary,
        int score,
        ExerciseSideSequence sideSequence,
        ExerciseInsectCompatibility insectCompatibility,
        ExerciseDirectionSequence directionSequence,
        params CanonicalMuscleGroup[] secondary)
    {
        return new Exercise
        {
            Id = id,
            Name = $"Exercise {id}",
            Video = $"exercise_{id:D4}.mp4",
            PrimaryCanonicalGroup = primary,
            SecondaryCanonicalGroups = secondary,
            Practice = "Test practice",
            MotionProfile = "Test motion",
            Mode = ExerciseMode.Repetition,
            Presentation = ExercisePresentation.Motion,
            HoldFramePercent = 0,
            SideSequence = sideSequence,
            DirectionSequence = directionSequence,
            SequenceBlocks = BuildSequenceBlocks(
                id,
                sideSequence,
                directionSequence),
            InsectCompatibility = insectCompatibility,
            HardFloorCompatibility = ExerciseHardFloorCompatibility.Compatible,
            UpperBodyClothingRequirement =
                ExerciseUpperBodyClothingRequirement.Agnostic,
            ShyCompatibility = ExerciseShyCompatibility.Compatible,
            MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
            Score = score,
            OnlyFeetTouchGround = true,
            ShoeAgnostic = true,
            MaxSpaceMeters = 3,
            Equipment = "None",
            Silent = true,
        };
    }

    private static ExerciseSequenceBlock[] BuildSequenceBlocks(
        int exerciseId,
        ExerciseSideSequence sideSequence,
        ExerciseDirectionSequence directionSequence)
    {
        (ExerciseSequenceSideCue Cue, bool Mirror)[] sides = sideSequence switch
        {
            ExerciseSideSequence.ScreenLeftThenRight =>
            [
                (ExerciseSequenceSideCue.ScreenLeft, false),
                (ExerciseSequenceSideCue.ScreenRight, true),
            ],
            ExerciseSideSequence.ScreenRightThenLeft =>
            [
                (ExerciseSequenceSideCue.ScreenRight, false),
                (ExerciseSequenceSideCue.ScreenLeft, true),
            ],
            ExerciseSideSequence.ScreenLeftLeadThenRightLead =>
            [
                (ExerciseSequenceSideCue.ShownLeadStance, false),
                (ExerciseSequenceSideCue.OppositeLeadStance, true),
            ],
            ExerciseSideSequence.ScreenRightLeadThenLeftLead =>
            [
                (ExerciseSequenceSideCue.ShownLeadStance, false),
                (ExerciseSequenceSideCue.OppositeLeadStance, true),
            ],
            _ => [(ExerciseSequenceSideCue.None, false)],
        };
        (ExerciseSequenceDirectionCue Cue, ExerciseSequenceMediaSegment Segment)[] directions =
            directionSequence switch
            {
                ExerciseDirectionSequence.ForwardThenBackward =>
                [
                    (ExerciseSequenceDirectionCue.Forward,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Backward,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                ExerciseDirectionSequence.BackwardThenForward =>
                [
                    (ExerciseSequenceDirectionCue.Backward,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Forward,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                ExerciseDirectionSequence.ClockwiseThenCounterclockwise =>
                [
                    (ExerciseSequenceDirectionCue.Clockwise,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Counterclockwise,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                ExerciseDirectionSequence.CounterclockwiseThenClockwise =>
                [
                    (ExerciseSequenceDirectionCue.Counterclockwise,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Clockwise,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                ExerciseDirectionSequence.InwardThenOutward =>
                [
                    (ExerciseSequenceDirectionCue.Inward,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Outward,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                ExerciseDirectionSequence.OutwardThenInward =>
                [
                    (ExerciseSequenceDirectionCue.Outward,
                        ExerciseSequenceMediaSegment.FirstDirection),
                    (ExerciseSequenceDirectionCue.Inward,
                        ExerciseSequenceMediaSegment.SecondDirection),
                ],
                _ =>
                [
                    (ExerciseSequenceDirectionCue.None,
                        ExerciseSequenceMediaSegment.Full),
                ],
            };

        return sides
            .SelectMany(side => directions.Select(direction =>
                new ExerciseSequenceBlock
                {
                    ExerciseId = exerciseId,
                    SideCue = side.Cue,
                    DirectionCue = direction.Cue,
                    MirrorMedia = side.Mirror,
                    MediaSegment = direction.Segment,
                }))
            .ToArray();
    }

    private static ExerciseSequenceBlock[] BuildLinkedSequenceBlocks(
        int firstExerciseId,
        int secondExerciseId,
        ExerciseSideSequence sideSequence)
    {
        ExerciseSequenceBlock[] first = BuildSequenceBlocks(
            firstExerciseId,
            sideSequence,
            ExerciseDirectionSequence.None);
        ExerciseSequenceBlock[] second = BuildSequenceBlocks(
            secondExerciseId,
            sideSequence,
            ExerciseDirectionSequence.None);
        return [.. first, .. second];
    }

    private static void CompleteRoundsBefore(
        ExerciseSessionService service,
        WorkoutState state,
        WorkoutGroup target)
    {
        foreach (WorkoutGroup round in service.GetActiveGroups(state)
                     .TakeWhile(round => round.Id != target.Id))
        {
            if (service.IsIntermediateSequenceBlock(state, round))
            {
                service.AdvanceSequence(state, round);
            }
            else
            {
                service.RecordOutcome(state, round, keep: true);
            }
        }
    }

    private static void CompleteRemainingRounds(
        ExerciseSessionService service,
        WorkoutState state)
    {
        while (service.GetNextGroup(state) is WorkoutGroup round)
        {
            if (service.IsIntermediateSequenceBlock(state, round))
            {
                service.AdvanceSequence(state, round);
            }
            else
            {
                service.RecordOutcome(state, round, keep: true);
            }
        }
    }

    private sealed class AlwaysZeroRandom : Random
    {
        public override int Next(int maxValue)
        {
            return 0;
        }
    }
}
