using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class CatalogCompletionTests
{
    [Theory]
    [InlineData(15, WorkoutModifiers.Insect)]
    [InlineData(15, WorkoutModifiers.Insect | WorkoutModifiers.HardFloor)]
    [InlineData(20, WorkoutModifiers.Insect | WorkoutModifiers.HardFloor)]
    [InlineData(30, WorkoutModifiers.Insect | WorkoutModifiers.HardFloor)]
    [InlineData(30, WorkoutModifiers.Insect | WorkoutModifiers.Silence)]
    [InlineData(30, WorkoutModifiers.Insect | WorkoutModifiers.Shy)]
    [InlineData(60, WorkoutModifiers.Insect | WorkoutModifiers.HardFloor | WorkoutModifiers.Silence | WorkoutModifiers.Shy)]
    public void AcceptedGapsPreserveFullDurationAndCompleteWorkouts(int minutes, WorkoutModifiers profile)
    {
        foreach (bool light in new[] { false, true })
        {
            var service = new ExerciseSessionService(LoadCatalog(), new Random(4));
            var state = new WorkoutState();
            WorkoutModifiers modifiers = profile | (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
            service.StartWorkout(state, minutes, modifiers);
            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Equal(minutes, rounds.Length);
            Assert.All(rounds, round =>
            {
                Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(round, modifiers));
                Exercise selected = service.GetSelectedExercise(state, round);
                Assert.True(WorkoutModifierPolicy.IsCompatible(selected, modifiers));
                Assert.True(WorkoutCoveragePolicy.IsSelectable(selected, round));
            });
            foreach (WorkoutGroup round in rounds)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Fact]
    public void AcceptedGapsApplyOnlyToTheirDeclaredModifierCombinations()
    {
        foreach (AcceptedWorkoutCoverageException exception in WorkoutModifierPolicy.AcceptedCoverageExceptions)
        {
            int minutes = int.Parse(exception.GroupId.Split('.')[0][1..]);
            WorkoutGroup group = MassGroupingTaxonomy.GetGroup(minutes, exception.GroupId);
            Assert.False(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, exception.RequiredModifiers));
            Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(group,
                exception.RequiredModifiers & ~WorkoutModifiers.Insect));
            Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, WorkoutModifiers.None));
            if (exception.RequiredModifiers != WorkoutModifiers.Insect)
                Assert.True(WorkoutModifierPolicy.IsSelectionGroupAvailable(group, WorkoutModifiers.Insect));
        }
    }

    private static Exercise[] LoadCatalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        })!;

    [Fact]
    public void RepeatedSpinalWaveFillsTwoForearmSlotsWithoutInventingRotatorCoverage()
    {
        Exercise[] wave = [LoadCatalog().Single(exercise => exercise.Id == 1032)];
        WorkoutGroup[] groups = new[] { "r30.forearm-flexors-pronators",
            "r30.forearm-extensors-supinators", "r30.rotator-cuff" }
            .Select(key => MassGroupingTaxonomy.GetGroup(30, key)).ToArray();
        const WorkoutModifiers profile = WorkoutModifiers.Insect | WorkoutModifiers.HardFloor;
        Assert.Equal(1, WorkoutModifierPolicy.GetMaximumDistinctLineupSize(wave, groups, profile, 3));
        Assert.Equal(2, WorkoutModifierPolicy.GetMaximumCompleteLineupSize(wave, groups, profile, 3));
        Assert.False(WorkoutCoveragePolicy.IsSelectable(wave[0], groups[2]));
    }

    [Theory]
    [InlineData(10, true, false, 1029, "r10.anterior-lateral-lower-leg-dorsal-foot")]
    [InlineData(10, true, true, 1029, "r10.anterior-lateral-lower-leg-dorsal-foot")]
    [InlineData(20, false, false, 1028, "r20.accessory-hip-adductors")]
    [InlineData(20, false, true, 1028, "r20.accessory-hip-adductors")]
    [InlineData(30, false, false, 1028, "r30.accessory-hip-adductors")]
    [InlineData(30, false, true, 1028, "r30.accessory-hip-adductors")]
    public void ReviewedHeelDigsAndChairSqueezeCompleteHardFloorWorkouts(
        int minutes, bool insect, bool light, int expectedId, string selectionKey)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] catalog = LoadCatalog();
            catalog.Single(exercise => exercise.Id == expectedId).Score = 10000;
            var service = new ExerciseSessionService(catalog, new Random(seed));
            var state = new WorkoutState();
            WorkoutModifiers profile = WorkoutModifiers.HardFloor | WorkoutModifiers.Silence |
                WorkoutModifiers.Shy | WorkoutModifiers.UpperBodyClothing |
                (insect ? WorkoutModifiers.Insect : WorkoutModifiers.None) |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
            service.StartWorkout(state, minutes, profile);
            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            WorkoutGroup target = rounds.Single(round => round.SelectionKey == selectionKey);
            Assert.Equal(expectedId, service.GetSelectedExercise(state, target).Id);
            Assert.All(rounds, round => Assert.True(WorkoutModifierPolicy.IsCompatible(
                service.GetSelectedExercise(state, round), profile)));
            WorkoutGroup[] baseRounds = rounds.Where(round => round.SequenceBlockIndex == 0).ToArray();
            Assert.Equal(baseRounds.Length, baseRounds.Select(round =>
                WorkoutModifierPolicy.GetSessionMovementId(service.GetSelectedExercise(state, round)))
                .Distinct().Count());
            foreach (WorkoutGroup round in rounds)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReverseRowingClosesTheDistinctElbowSlotInAnInsectWorkout(bool light)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] catalog = LoadCatalog();
            Exercise row = catalog.Single(exercise => exercise.Id == 1030);
            Assert.Equal(CanonicalMuscleGroup.ElbowFlexors, row.PrimaryCanonicalGroup);
            Assert.Empty(row.SecondaryCanonicalGroups);
            Assert.Equal(0, row.MuscularDemand);
            Assert.Single(row.SequenceBlocks);
            Assert.True(WorkoutModifierPolicy.IsCompatible(row, WorkoutModifiers.Insect |
                WorkoutModifiers.HardFloor | WorkoutModifiers.Silence | WorkoutModifiers.Shy));
            Assert.True(WorkoutCoveragePolicy.IsSelectable(row,
                MassGroupingTaxonomy.GetGroup(30, "r30.elbow-flexors")));
            Assert.False(WorkoutCoveragePolicy.IsSelectable(row,
                MassGroupingTaxonomy.GetGroup(3, "r3.head-neck-upper-limbs")));
            var state = new WorkoutState();
            var service = new ExerciseSessionService(catalog, new Random(seed));
            WorkoutModifiers profile = WorkoutModifiers.Insect |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
            service.StartWorkout(state, 30, profile);
            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Equal(1030, service.GetSelectedExercise(state,
                rounds.Single(round => round.SelectionKey == "r30.elbow-flexors")).Id);
            WorkoutGroup[] roots = rounds.Where(round => round.SequenceBlockIndex == 0).ToArray();
            Assert.Equal(roots.Length, roots.Select(round => WorkoutModifierPolicy.GetSessionMovementId(
                service.GetSelectedExercise(state, round))).Distinct().Count());
            Assert.All(rounds, round => Assert.True(WorkoutModifierPolicy.IsCompatible(
                service.GetSelectedExercise(state, round), profile)));
            foreach (WorkoutGroup round in rounds)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChairSquatSqueezeSuppliesHardFloorInsectAdductorSlotsWithoutDuplicateFamily(bool light)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] catalog = LoadCatalog();
            Exercise chair = catalog.Single(exercise => exercise.Id == 1031);
            Assert.Equal(969, chair.SessionMovementId);
            Assert.Single(chair.SequenceBlocks);
            WorkoutModifiers profile = WorkoutModifiers.HardFloor | WorkoutModifiers.Insect |
                WorkoutModifiers.Silence | WorkoutModifiers.Shy | WorkoutModifiers.UpperBodyClothing |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
            Assert.True(WorkoutModifierPolicy.IsCompatible(chair, profile));
            foreach (int minutes in new[] { 20, 30 })
                Assert.True(WorkoutCoveragePolicy.IsSelectable(chair,
                    MassGroupingTaxonomy.GetGroup(minutes, $"r{minutes}.accessory-hip-adductors")));
            Assert.False(WorkoutModifierPolicy.IsCompatible(
                catalog.Single(exercise => exercise.Id == 1028), profile));

            chair.Score = 10000;
            var state = new WorkoutState();
            var service = new ExerciseSessionService(catalog, new Random(seed));
            service.StartWorkout(state, 10, profile);
            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Contains(rounds, round => service.GetSelectedExercise(state, round).Id == 1031);
            WorkoutGroup[] roots = rounds.Where(round => round.SequenceBlockIndex == 0).ToArray();
            Assert.Equal(roots.Length, roots.Select(round => WorkoutModifierPolicy.GetSessionMovementId(
                service.GetSelectedExercise(state, round))).Distinct().Count());
            Assert.All(rounds, round => Assert.True(WorkoutModifierPolicy.IsCompatible(
                service.GetSelectedExercise(state, round), profile)));
            foreach (WorkoutGroup round in rounds)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SpinalWaveSuppliesHardFloorInsectForearmSlotsAndCompletesAUniqueLineup(bool light)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] catalog = LoadCatalog();
            Exercise wave = catalog.Single(exercise => exercise.Id == 1032);
            WorkoutModifiers profile = WorkoutModifiers.HardFloor | WorkoutModifiers.Insect |
                WorkoutModifiers.Silence | WorkoutModifiers.Shy | WorkoutModifiers.UpperBodyClothing |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None);
            Assert.True(WorkoutModifierPolicy.IsCompatible(wave, profile));
            foreach ((int minutes, string key) in new[]
            {
                (15, "r15.arm-forearm-hand"), (20, "r20.forearm-hand"),
                (30, "r30.forearm-flexors-pronators"), (30, "r30.forearm-extensors-supinators"),
            })
                Assert.True(WorkoutCoveragePolicy.IsSelectable(wave,
                    MassGroupingTaxonomy.GetGroup(minutes, key)));
            Assert.NotEqual(1026, WorkoutModifierPolicy.GetSessionMovementId(wave));

            wave.Score = 10000;
            var state = new WorkoutState();
            var service = new ExerciseSessionService(catalog, new Random(seed));
            service.StartWorkout(state, 10, profile);
            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Contains(rounds, round => service.GetSelectedExercise(state, round).Id == 1032);
            WorkoutGroup[] roots = rounds.Where(round => round.SequenceBlockIndex == 0).ToArray();
            Assert.Equal(roots.Length, roots.Select(round => WorkoutModifierPolicy.GetSessionMovementId(
                service.GetSelectedExercise(state, round))).Distinct().Count());
            Assert.All(rounds, round => Assert.True(WorkoutModifierPolicy.IsCompatible(
                service.GetSelectedExercise(state, round), profile)));
            foreach (WorkoutGroup round in rounds)
            {
                service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
                if (service.IsIntermediateSequenceBlock(state, round)) service.AdvanceSequence(state, round);
                else service.RecordOutcome(state, round, keep: true);
                service.ClearPendingRest(state);
            }
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Fact]
    public void NewDirectMovementsPreserveExistingAnatomySequenceAndFeedback()
    {
        Exercise[] catalog = LoadCatalog();
        Exercise chair = catalog.Single(exercise => exercise.Id == 1028);
        Exercise heel = catalog.Single(exercise => exercise.Id == 1029);
        Assert.Equal(CanonicalMuscleGroup.MedialAndDeepKneeExtensors, chair.PrimaryCanonicalGroup);
        Assert.Contains(CanonicalMuscleGroup.AccessoryHipAdductors, chair.SecondaryCanonicalGroups);
        Assert.Equal(2, chair.MuscularDemand);
        Assert.Equal(969, chair.SessionMovementId);
        Assert.Equal(969, catalog.Single(exercise => exercise.Id == 969).SessionMovementId);
        Assert.Equal(new[] { 784, 969, 1000 }, catalog.Single(exercise => exercise.Id == 784)
            .SequenceBlocks.Select(block => block.ExerciseId));
        Assert.Equal(CanonicalMuscleGroup.HipFlexors, heel.PrimaryCanonicalGroup);
        Assert.Equal(new[] { CanonicalMuscleGroup.AnteriorLateralLowerLegAndDorsalFoot },
            heel.SecondaryCanonicalGroups);
        Assert.Equal(1, heel.MuscularDemand);
        Assert.Equal(ExerciseSideSequence.Alternating, heel.SideSequence);
        Assert.Single(chair.SequenceBlocks);
        Assert.Single(heel.SequenceBlocks);
        Assert.False(WorkoutModifierPolicy.IsCompatible(chair, WorkoutModifiers.Insect));
        Assert.False(WorkoutModifierPolicy.IsCompatible(
            catalog.Single(exercise => exercise.Id == 194), WorkoutModifiers.HardFloor));

        Dictionary<int, StoredExerciseSnapshot> stored = catalog
            .Where(exercise => exercise.Id is not (1028 or 1029))
            .ToDictionary(exercise => exercise.Id, exercise =>
                new StoredExerciseSnapshot(exercise.Name, exercise.Video, exercise.Id % 41 - 20));
        Assert.Equal(stored.Keys.ToHashSet(), CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
        var state = new WorkoutState
        {
            CatalogRevision = 75,
            SelectedExerciseIds = new() { ["r30.rotator-cuff"] = 1026 },
            KeptExerciseRootIdsBySelectionGroupId = new() { ["r30.rotator-cuff"] = [1026] },
            LastKeptExerciseIds = [1026],
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(state, catalog.ToDictionary(exercise => exercise.Id)));
        Assert.Equal(1026, state.SelectedExerciseIds["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 }, state.KeptExerciseRootIdsBySelectionGroupId["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 }, state.LastKeptExerciseIds);
    }

    [Theory]
    [InlineData(480)]
    [InlineData(517)]
    public void BreathOfJoyInsectCorrectionPreservesIdentityAndFeedback(int exerciseId)
    {
        Exercise[] catalog = LoadCatalog();
        Exercise exercise = catalog.Single(item => item.Id == exerciseId);
        WorkoutGroup breathing = MassGroupingTaxonomy.GetGroup(30, "r30.breathing-muscles");
        Assert.True(WorkoutModifierPolicy.IsCompatible(exercise,
            WorkoutModifiers.Insect | WorkoutModifiers.HardFloor));
        Assert.False(WorkoutModifierPolicy.IsCompatible(exercise,
            WorkoutModifiers.Insect | WorkoutModifiers.Silence));
        Assert.False(WorkoutModifierPolicy.IsCompatible(exercise,
            WorkoutModifiers.Insect | WorkoutModifiers.Shy));
        Assert.True(WorkoutCoveragePolicy.IsSelectable(exercise, breathing));
        Assert.Equal(CanonicalMuscleGroup.BreathingMuscles, exercise.PrimaryCanonicalGroup);
        Assert.Equal(480, WorkoutModifierPolicy.GetSessionMovementId(exercise));
        Assert.Single(exercise.SequenceBlocks);
        Assert.Equal(0, exercise.MuscularDemand);

        Dictionary<int, StoredExerciseSnapshot> stored = catalog.ToDictionary(
            item => item.Id, item => new StoredExerciseSnapshot(item.Name, item.Video, item.Id % 41 - 20));
        Assert.Equal(stored.Keys.ToHashSet(),
            CatalogMigrationRules.ValidatePreservedCatalog(catalog, stored));
        var state = new WorkoutState
        {
            CatalogRevision = 74,
            SelectedExerciseIds = new() { [breathing.SelectionKey] = exerciseId },
            KeptExerciseRootIdsBySelectionGroupId = new()
                { [breathing.SelectionKey] = [exerciseId] },
            LastKeptExerciseIds = [exerciseId],
            LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle = new()
                { [nameof(CanonicalMuscleGroup.BreathingMuscles)] = 123456 },
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(
            state, catalog.ToDictionary(item => item.Id)));
        Assert.Equal(exerciseId, state.SelectedExerciseIds[breathing.SelectionKey]);
        Assert.Equal(new HashSet<int> { exerciseId },
            state.KeptExerciseRootIdsBySelectionGroupId[breathing.SelectionKey]);
        Assert.Equal(new HashSet<int> { exerciseId }, state.LastKeptExerciseIds);
        Assert.Equal(123456,
            state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle[nameof(CanonicalMuscleGroup.BreathingMuscles)]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void DefaultShortWorkoutRetainsBroadTrainingWithAndWithoutLight(
        bool light,
        bool shy,
        bool insect)
    {
        foreach (int seed in new[] { 1, 2, 4 })
        {
            Exercise[] exercises = LoadCatalog();
            // A liked isolated wrist movement must never occupy the broad
            // upper-body round, including when Light prioritizes mobility.
            exercises.Single(exercise => exercise.Id == 239).Score = 100;
            var service = new ExerciseSessionService(exercises, new Random(seed));
            var state = new WorkoutState();
            WorkoutModifiers profile = state.LastWorkoutModifiers |
                (light ? WorkoutModifiers.Light : WorkoutModifiers.None) |
                (shy ? WorkoutModifiers.Shy : WorkoutModifiers.None) |
                (insect ? WorkoutModifiers.Insect : WorkoutModifiers.None);

            service.StartWorkout(state, 3, profile);

            WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
            Assert.Equal(3, rounds.Length);
            Assert.Equal(3, rounds.Select(round =>
                WorkoutModifierPolicy.GetSessionMovementId(
                    service.GetSelectedExercise(state, round))).Distinct().Count());
            foreach (WorkoutGroup round in rounds)
            {
                Exercise selected = service.GetSelectedExercise(state, round);
                Assert.True(WorkoutCoveragePolicy.IsSelectable(selected, round));
                Assert.True(WorkoutModifierPolicy.IsCompatible(selected, profile));
                Assert.Single(selected.SequenceBlocks);
            }
            WorkoutGroup upper = rounds.Single(round => round.SelectionKey == "r3.head-neck-upper-limbs");
            Exercise upperExercise = service.GetSelectedExercise(state, upper);
            Assert.True(WorkoutCoveragePolicy.IsSelectable(upperExercise, upper));
            Assert.NotEqual(239, upperExercise.Id);
            if (light && !insect) Assert.Equal(0, upperExercise.MuscularDemand);
            foreach (WorkoutGroup round in rounds)
                service.RecordOutcome(state, round, keep: true);
            Assert.True(state.WorkoutCompleted);
        }
    }

    [Fact]
    public void CompoundWorkoutFeedbackAndHistorySurviveReload()
    {
        Exercise[] catalog = LoadCatalog();
        var service = new ExerciseSessionService(catalog, new Random(1));
        var state = new WorkoutState { CatalogRevision = CatalogMigrationRules.CurrentCatalogRevision };
        WorkoutModifiers profile = state.LastWorkoutModifiers | WorkoutModifiers.Insect | WorkoutModifiers.Shy;
        service.StartWorkout(state, 3, profile);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        WorkoutGroup upper = rounds.Single(round => round.SelectionKey == "r3.head-neck-upper-limbs");
        Assert.Equal(248, service.GetSelectedExercise(state, upper).Id);
        foreach (WorkoutGroup round in rounds)
        {
            service.BeginRest(state, round, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
            Assert.True(service.KeepPendingRest(state));
            service.RecordOutcome(state, round, keep: true);
            service.ClearPendingRest(state);
        }
        Assert.True(state.WorkoutCompleted);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        string hardRecovery = JsonSerializer.Serialize(state.LastHardWorkUnixMillisecondsByPrimaryMuscle);
        string meaningfulRecovery = JsonSerializer.Serialize(state.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle);
        Dictionary<int, int> scores = catalog.ToDictionary(exercise => exercise.Id, exercise => exercise.Score);
        int version = state.Version;
        int revision = state.CatalogRevision;

        WorkoutState reloaded = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
        var resumed = new ExerciseSessionService(catalog, new Random(2));
        resumed.Initialize(reloaded);
        Assert.Equal(history, JsonSerializer.Serialize(reloaded.WorkoutHistory));
        Assert.Equal(keeps, JsonSerializer.Serialize(reloaded.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(hardRecovery, JsonSerializer.Serialize(reloaded.LastHardWorkUnixMillisecondsByPrimaryMuscle));
        Assert.Equal(meaningfulRecovery, JsonSerializer.Serialize(reloaded.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle));
        Assert.All(catalog, exercise => Assert.Equal(scores[exercise.Id], exercise.Score));
        Assert.Equal(version, reloaded.Version);
        Assert.Equal(revision, reloaded.CatalogRevision);
        Assert.Contains(reloaded.WorkoutHistory.Single().Decisions, decision =>
            decision.SelectionGroupId == upper.SelectionKey &&
            decision.RootExerciseId == 248 && decision.Outcome == ExerciseOutcome.Tick);

        // Done applies the recorded decisions to preferences for the next workout.
        resumed.AcknowledgeCompletion(reloaded);
        Assert.Contains(248, reloaded.KeptExerciseRootIdsBySelectionGroupId[upper.SelectionKey]);
        string acknowledgedKeeps = JsonSerializer.Serialize(reloaded.KeptExerciseRootIdsBySelectionGroupId);
        WorkoutState acknowledged = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(reloaded))!;
        new ExerciseSessionService(catalog, new Random(3)).Initialize(acknowledged);
        Assert.Equal(acknowledgedKeeps, JsonSerializer.Serialize(acknowledged.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Equal(history, JsonSerializer.Serialize(acknowledged.WorkoutHistory));
        Assert.Equal(hardRecovery, JsonSerializer.Serialize(acknowledged.LastHardWorkUnixMillisecondsByPrimaryMuscle));
        Assert.Equal(meaningfulRecovery, JsonSerializer.Serialize(acknowledged.LastMeaningfulWorkUnixMillisecondsByPrimaryMuscle));
    }

    [Fact]
    public void PlantedTeacupAdmissionDoesNotResetExistingCatalogFeedback()
    {
        Exercise[] catalog = LoadCatalog();
        Dictionary<int, StoredExerciseSnapshot> stored = catalog
            .Where(exercise => exercise.Id != 1027)
            .ToDictionary(exercise => exercise.Id, exercise =>
                new StoredExerciseSnapshot(exercise.Name, exercise.Video,
                    Score: exercise.Id % 41 - 20));
        IReadOnlySet<int> preserved = CatalogMigrationRules.ValidatePreservedCatalog(
            catalog, stored);
        Assert.Equal(stored.Keys.ToHashSet(), preserved);
        Assert.DoesNotContain(1027, preserved);

        var state = new WorkoutState
        {
            CatalogRevision = 73,
            SelectedExerciseIds = new() { ["r30.rotator-cuff"] = 1026 },
            KeptExerciseRootIdsBySelectionGroupId = new()
                { ["r30.rotator-cuff"] = [1026] },
            LastKeptExerciseIds = [1026],
            LastHardWorkUnixMillisecondsByPrimaryMuscle = new()
                { [nameof(CanonicalMuscleGroup.RotatorCuff)] = 123456 },
        };
        Assert.True(CatalogMigrationRules.ReconcileWorkoutState(
            state, catalog.ToDictionary(exercise => exercise.Id)));
        Assert.Equal(1026, state.SelectedExerciseIds["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 },
            state.KeptExerciseRootIdsBySelectionGroupId["r30.rotator-cuff"]);
        Assert.Equal(new HashSet<int> { 1026 }, state.LastKeptExerciseIds);
        Assert.Equal(123456,
            state.LastHardWorkUnixMillisecondsByPrimaryMuscle[nameof(CanonicalMuscleGroup.RotatorCuff)]);
        Assert.Equal(CatalogMigrationRules.CurrentCatalogRevision, state.CatalogRevision);
    }
}
