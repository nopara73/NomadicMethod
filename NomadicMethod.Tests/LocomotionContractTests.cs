using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Nodes;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class LocomotionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        JsonOptions)!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplitSeriesRetainsEveryChosenMemberAndPhaseFeedback(bool active)
    {
        Exercise[] catalog = Catalog();
        string slot = MassGroupingTaxonomy.GetResolution(30).Groups.Single(g =>
            g.CanonicalGroups.Contains(CanonicalMuscleGroup.ShoulderAbductors)).Id;
        var state = new WorkoutState { CatalogRevision = 79,
            ActiveCatalogRevision = active ? 79 : 0, ActiveWorkoutMinutes = active ? 3 : 0 };
        state.KeptExerciseRootIdsBySelectionGroupId[slot] = [459];
        state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance] = new() { [459] = -3 };
        if (!active) state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance][469] = -7;
        var service = new ExerciseSessionService(catalog, new Random(5));
        service.Initialize(state);
        if (active)
        {
            Assert.Equal(new HashSet<int> { 459 }, state.KeptExerciseRootIdsBySelectionGroupId[slot]);
            service.EndActiveWorkout(state);
        }
        Assert.Equal(new HashSet<int> { 459, 468, 469 }, state.KeptExerciseRootIdsBySelectionGroupId[slot]);
        var feedback = state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance];
        Assert.Equal(-3, feedback[459]);
        Assert.Equal(-3, feedback[468]);
        Assert.Equal(active ? -3 : -7, feedback[469]);
        feedback[459] = -8;
        service.Initialize(state);
        Assert.Equal(-3, state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance][468]);
        Assert.Equal(0, state.ActiveCatalogRevision);

        var current = new WorkoutState { CatalogRevision = 80,
            ActiveCatalogRevision = 80, ActiveWorkoutMinutes = 3 };
        current.KeptExerciseRootIdsBySelectionGroupId[slot] = [459];
        current.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance] = new() { [459] = -4 };
        service.Initialize(current);
        service.EndActiveWorkout(current);
        Assert.Equal(new HashSet<int> { 459 }, current.KeptExerciseRootIdsBySelectionGroupId[slot]);
        Assert.False(current.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance].ContainsKey(468));
    }

    [Theory]
    [InlineData("Ready")]
    [InlineData("Movement")]
    [InlineData("Rest")]
    public void CatalogUpgradePreservesTheOriginalCheckpointAndHandSequence(string phase)
    {
        Exercise[] catalog = Catalog();
        Exercise[] old = catalog.Where(e => new[] { 21, 37, 216 }.Contains(e.Id)).Select(e =>
        {
            JsonObject json = JsonSerializer.SerializeToNode(e, JsonOptions)!.AsObject();
            json["locomotionSpace"] = "None";
            json["maxSpaceMeters"] = 2;
            if (e.Id == 216)
            {
                json["sideSequence"] = "Continuous";
                json["sequenceBlocks"] = JsonSerializer.SerializeToNode(new[] {
                    new ExerciseSequenceBlock { ExerciseId = 216, MirrorMedia = false } }, JsonOptions);
            }
            return json.Deserialize<Exercise>(JsonOptions)!;
        }).ToArray();
        DateTimeOffset now = new(2026, 10, 14, 6, 0, 0, TimeSpan.Zero);
        var state = new WorkoutState();
        var legacy = new ExerciseSessionService(old, new Random(4), () => now);
        legacy.Initialize(state);
        legacy.StartWorkout(state, 3, WorkoutModifiers.None);
        WorkoutGroup next = legacy.GetNextGroup(state)!;
        Assert.Equal(216, legacy.GetSelectedExercise(state, next).Id);
        if (phase == "Movement") legacy.BeginMovement(state, next, 28_000, now.ToUnixTimeMilliseconds() + 28_000);
        if (phase == "Rest") legacy.BeginRest(state, next, now.ToUnixTimeMilliseconds() + 14_000);
        state.CatalogRevision = 79;
        state.ActiveCatalogRevision = 0;
        long sessionId = state.ActiveWorkoutSession!.SessionId;
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks);
        for (int reopen = 0; reopen < 2; reopen++)
        {
            state = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
            var service = new ExerciseSessionService(catalog, new Random(7), () => now.AddHours(reopen + 1));
            service.RestoreAfterReopen(state);
            if (phase == "Movement") Assert.Equal(28_000, state.PendingMovementMillisecondsRemaining);
            Assert.Equal(216, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
            Assert.Equal(3, service.GetActiveGroups(state).Count);
            Assert.Equal(sessionId, state.ActiveWorkoutSession!.SessionId);
            Assert.Equal(blocks, JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks));
            Assert.Equal(79, state.ActiveCatalogRevision);
            Assert.Equal(LocomotionSpace.Room, WorkoutModifierPolicy.GetLocomotionSpace(state.ActiveWorkoutModifiers));
            Assert.Equal(WorkoutModifiers.None, state.LastWorkoutModifiers);
        }
        Assert.Equal(2, catalog.Single(e => e.Id == 216).SequenceBlocks.Length);
    }

    [Fact]
    public void TravelIsAnInclusiveThreeStateSpaceAllowance()
    {
        WorkoutModifiers other = WorkoutModifiers.Shy | WorkoutModifiers.Light |
            WorkoutModifiers.Wall | WorkoutModifiers.Silence;
        Exercise[] catalog = Catalog();
        foreach (LocomotionSpace capacity in Enum.GetValues<LocomotionSpace>())
        {
            WorkoutModifiers profile = WorkoutModifierPolicy.WithLocomotionSpace(other, capacity);
            Assert.Equal(capacity, WorkoutModifierPolicy.GetLocomotionSpace(profile));
            Assert.Equal(other, profile & ~(WorkoutModifiers.RoomLocomotion | WorkoutModifiers.FieldLocomotion));
            foreach (Exercise exercise in catalog)
            {
                WorkoutModifiers unrestricted = WorkoutModifierPolicy.WithLocomotionSpace(
                    WorkoutModifiers.None, capacity);
                if (exercise.LocomotionSpace > capacity)
                    Assert.False(WorkoutModifierPolicy.IsCompatible(exercise, unrestricted));
                else if (!exercise.WallRequired && exercise.MirrorRelationship != ExerciseMirrorRelationship.MirrorOnly)
                    Assert.True(WorkoutModifierPolicy.IsCompatible(exercise, unrestricted));
            }
        }
        Assert.Equal(LocomotionSpace.None, WorkoutModifierPolicy.GetLocomotionSpace(WorkoutModifiers.FieldLocomotion));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WorkoutModifierPolicy.WithLocomotionSpace(other, (LocomotionSpace)99));
    }

    [Fact]
    public void LocomotionMinimumsCountApprovedWholeMovementsAndRejectCatalogLoss()
    {
        Exercise[] catalog = Catalog();
        Assert.All(catalog, exercise => Assert.True(WorkoutModifierPolicy.IsSpaceMetadataValid(exercise)));
        Assert.Empty(WorkoutModifierPolicy.FindLocomotionCatalogDeficiencies(catalog));
        foreach ((LocomotionSpace space, int minimum) in new[]
                 { (LocomotionSpace.Room, 25), (LocomotionSpace.Field, 5) })
        {
            Exercise[] roots = catalog.Where(exercise => exercise.LocomotionSpace == space &&
                exercise.SequenceBlocks.Length > 0).DistinctBy(WorkoutModifierPolicy.GetSessionMovementId).ToArray();
            Assert.True(roots.Length >= minimum);
            HashSet<int> retained = roots.Take(minimum - 1).Select(WorkoutModifierPolicy.GetSessionMovementId).ToHashSet();
            Exercise[] undersized = catalog.Where(exercise => exercise.LocomotionSpace != space ||
                retained.Contains(WorkoutModifierPolicy.GetSessionMovementId(exercise))).ToArray();
            WorkoutLocomotionCatalogDeficiency failure = Assert.Single(
                WorkoutModifierPolicy.FindLocomotionCatalogDeficiencies(undersized));
            Assert.Equal(space, failure.LocomotionSpace);
            Assert.Equal(minimum - 1, failure.MatchingSessionMovementCount);
            Assert.Equal(minimum, failure.RequiredSessionMovementCount);
        }
        // Two timed hand configurations are still one locomotive exercise.
        Assert.Equal(2, catalog.Single(exercise => exercise.Id == 32).SequenceBlocks.Length);
    }

    [Fact]
    public void CrossedArmsAndInterlacedClaspsCompleteBothConfigurationsAtomically()
    {
        Exercise[] catalog = Catalog();
        foreach (int id in new[] { 32, 216, 224, 510, 740, 1012 })
        {
            Exercise exercise = catalog.Single(candidate => candidate.Id == id);
            Assert.Equal(ExerciseSideSequence.ShownHandPositionThenOpposite, exercise.SideSequence);
            Assert.True(exercise.SideSequence.UsesTimedSides());
            Assert.Equal(new[] { ExerciseSequenceSideCue.ShownHandPosition,
                ExerciseSequenceSideCue.OppositeHandPosition }, exercise.SequenceBlocks.Select(block => block.SideCue));
            Assert.Equal(new[] { false, true }, exercise.SequenceBlocks.Select(block => block.MirrorMedia));
            Assert.All(exercise.SequenceBlocks, block => Assert.Equal(id, block.ExerciseId));
        }
    }

    [Fact]
    public void ShyReviewExcludesRelatedConspicuousCombinations()
    {
        Exercise[] catalog = Catalog();
        foreach (int id in new[] { 432, 435, 436, 437, 438, 439, 440, 441, 443,
                 446, 447, 452, 453, 458, 460, 461, 468, 469, 471, 472, 476, 479, 494,
                 496, 547, 1002, 1025, 1026, 1027, 1042, 1044, 1048, 1049, 1055, 1056 })
        {
            Exercise exercise = catalog.Single(candidate => candidate.Id == id);
            Assert.Equal(ExerciseShyCompatibility.Incompatible, exercise.ShyCompatibility);
            Assert.False(WorkoutModifierPolicy.IsCompatible(exercise,
                WorkoutModifierPolicy.WithLocomotionSpace(WorkoutModifiers.Shy, LocomotionSpace.Field)));
        }
    }
}
