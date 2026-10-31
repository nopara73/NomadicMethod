using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutVarietyPlanningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static JsonElement[] Cases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Assets", "workout-variety-cases.json")));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.Clone()).ToArray();
    }

    public static IEnumerable<object[]> Names() => Cases().Select(c => new object[] { c.GetProperty("name").GetString()! });

    [Theory]
    [MemberData(nameof(Names))]
    public void DistinctSelectionsAndRepeatFallbackFollowTheSameContract(string name)
    {
        JsonElement item = Cases().Single(c => c.GetProperty("name").GetString() == name);
        var (service, state, _) = Start(item);
        var selections = state.ActiveWorkoutSession!.InitialSelections;
        Assert.Equal(item.GetProperty("minutes").GetInt32(), service.GetActiveGroups(state).Count);
        Assert.Equal(item.GetProperty("expectedSelections").GetInt32(), selections.Count);
        Assert.Equal(item.GetProperty("expectedMaxSets").GetInt32(), selections.Max(s => s.SetCount));
        Assert.Equal(item.GetProperty("expectedFirstSets").GetInt32(), selections.Single(s => s.RootExerciseId == 1000).SetCount);
        Assert.Equal(selections.Count, service.GetActiveGroups(state).Select(g =>
            WorkoutModifierPolicy.GetSessionMovementId(service.GetSelectedExercise(state, g))).Distinct().Count());
        if (item.GetProperty("light").GetBoolean())
            Assert.All(service.GetActiveGroups(state), g => Assert.Equal(0, service.GetSelectedExercise(state, g).MuscularDemand));
        Assert.Contains(1000, state.KeptExerciseRootIdsBySelectionGroupId[MassGroupingTaxonomy.GetResolution(30).Groups[0].Id]);
    }

    [Fact]
    public void AdditionalSelectionsAndReadySurviveColdReopenUnchanged()
    {
        var (service, state, catalog) = Start(Cases()[0]);
        string rounds = JsonSerializer.Serialize(service.GetActiveGroups(state));
        string history = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        WorkoutState saved = JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state, JsonOptions), JsonOptions)!;
        var restored = new ExerciseSessionService(catalog, new Random(123),
            () => DateTimeOffset.Parse("2026-11-07T01:00:00Z"));
        restored.Initialize(saved);
        Assert.Equal(2, saved.ActiveCoverageAccountingVersion);
        Assert.Equal(rounds, JsonSerializer.Serialize(restored.GetActiveGroups(saved)));
        Assert.Equal(history, JsonSerializer.Serialize(saved.ActiveWorkoutSession));
        Assert.Null(saved.PendingMovementGroupId);
    }

    [Fact]
    public void DurationEditRetainsTheCurrentAdditionalSelectionAndCheckpoint()
    {
        var (service, state, _) = Start(Cases()[0]);
        WorkoutGroup current = service.GetNextGroup(state)!;
        state.PendingMovementGroupId = current.Id;
        state.PendingMovementMillisecondsRemaining = 12345;
        state.PendingMovementPausedByUser = true;
        service.ResizeActiveWorkout(state, 15);
        Assert.Equal(15, service.GetActiveGroups(state).Count);
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(12345, state.PendingMovementMillisecondsRemaining);
        Assert.Single(state.ActiveWorkoutSession!.DurationChanges);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void OldAllocationVersionsAreNotUpgradedOnReopen(int version)
    {
        var (service, state, catalog) = Start(Cases()[1]);
        state.ActiveCoverageAccountingVersion = version;
        string rounds = JsonSerializer.Serialize(service.GetActiveGroups(state));
        var restored = new ExerciseSessionService(catalog, new Random(1));
        restored.Initialize(state);
        Assert.Equal(version, state.ActiveCoverageAccountingVersion);
        Assert.Equal(rounds, JsonSerializer.Serialize(restored.GetActiveGroups(state)));
    }

    [Fact]
    public void ModifierChangesRetainTheDistinctPartitionAndWholeDuration()
    {
        var (service, state, _) = Start(Cases()[0]);
        string[] slots = state.ActiveDurationSelectionGroupIds!.ToArray();
        service.ReconfigureActiveWorkout(state, WorkoutModifiers.Silence, service.GetNextGroup(state)!.Id);
        Assert.Equal(slots, state.ActiveDurationSelectionGroupIds);
        Assert.Equal(45, service.GetActiveGroups(state).Count);
        Assert.Equal(45, service.GetActiveGroups(state).DistinctBy(g => g.SelectionKey).Count());
        Assert.All(state.ActiveSetCountsBySelectionGroupId.Values, sets => Assert.Equal(1, sets));
    }

    [Fact]
    public void EarnedKeepsFollowAdditionalSelectionsIntoTheirActualSlots()
    {
        var (_, _, catalog) = Start(Cases()[0], wholeBodyCoverage: true);
        var service = new ExerciseSessionService(catalog, new Random(1));
        var state = new WorkoutState();
        service.StartWorkout(state, 3);
        int[] earned = service.GetActiveGroups(state).Select(g => service.GetSelectedExercise(state, g).Id).Distinct().ToArray();
        while (service.GetNextGroup(state) is { } next) service.RecordOutcome(state, next, true);
        service.AcknowledgeCompletion(state);
        service.StartWorkout(state, 45);
        var activeSlots = state.ActiveDurationSelectionGroupIds!.ToHashSet(StringComparer.Ordinal);
        var activeKeeps = state.KeptExerciseRootIdsBySelectionGroupId
            .Where(entry => activeSlots.Contains(entry.Key)).SelectMany(entry => entry.Value).ToHashSet();
        Assert.All(earned, id => Assert.Contains(id, activeKeeps));
        Assert.Equal(45, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void PreparedDistinctLineupSurvivesReopenBeforeActivation()
    {
        var (_, _, catalog) = Start(Cases()[0]);
        var service = new ExerciseSessionService(catalog, new Random(1));
        var state = new WorkoutState();
        service.PrepareWorkout(state, 45, WorkoutModifiers.None);
        string rounds = JsonSerializer.Serialize(service.GetActiveGroups(state));
        var restored = new ExerciseSessionService(catalog, new Random(123));
        restored.Initialize(state);
        Assert.Equal(rounds, JsonSerializer.Serialize(restored.GetActiveGroups(state)));
        Assert.Empty(state.ActiveWorkoutSession!.Blocks);
    }

    private static (ExerciseSessionService Service, WorkoutState State, Exercise[] Catalog) Start(JsonElement item, bool wholeBodyCoverage = false)
    {
        int copies = item.GetProperty("copies").GetInt32();
        bool aliases = item.GetProperty("aliases").GetBoolean(), light = item.GetProperty("light").GetBoolean();
        Exercise[] catalog = MassGroupingTaxonomy.GetResolution(30).Groups.SelectMany((g, index) =>
            Enumerable.Range(0, copies).Select(copy =>
            {
                int id = 1000 + index * 10 + copy;
                int blocks = item.GetProperty(index == 0 ? "firstBlocks" : "otherBlocks").GetInt32();
                return new Exercise
                {
                    Id = id, Name = $"Exercise {id}", Video = $"exercise_{id}.mp4",
                    PrimaryCanonicalGroup = g.CanonicalGroups.First(),
                    SecondaryCanonicalGroups = wholeBodyCoverage
                        ? MassGroupingTaxonomy.GetResolution(30).Groups.SelectMany(group => group.CanonicalGroups)
                            .Distinct().Where(muscle => muscle != g.CanonicalGroups.First()).ToArray() : [],
                    SessionMovementId = aliases ? 1000 + index * 10 : id,
                    Score = 0, MuscularDemand = light && copy == 1 ? 2 : 0,
                    Practice = "Test", MotionProfile = "Test", Mode = ExerciseMode.Repetition,
                    Presentation = ExercisePresentation.Motion, HoldFramePercent = 0,
                    SideSequence = blocks == 2 ? ExerciseSideSequence.ScreenLeftThenRight : ExerciseSideSequence.Continuous,
                    SequenceBlocks = Enumerable.Range(0, blocks).Select(b => new ExerciseSequenceBlock
                    {
                        ExerciseId = id, SideCue = blocks == 2 ? b == 0 ? ExerciseSequenceSideCue.ScreenLeft : ExerciseSequenceSideCue.ScreenRight : ExerciseSequenceSideCue.None,
                        MirrorMedia = b == 1,
                    }).ToArray(),
                    OnlyFeetTouchGround = true, ShoeAgnostic = true, MaxSpaceMeters = 2, Equipment = "None", Silent = true,
                    HardFloorCompatibility = ExerciseHardFloorCompatibility.Compatible,
                    MirrorRelationship = ExerciseMirrorRelationship.Agnostic,
                    UpperBodyClothingRequirement = ExerciseUpperBodyClothingRequirement.Agnostic,
                    ShyCompatibility = ExerciseShyCompatibility.Compatible,
                };
            })).ToArray();
        var state = new WorkoutState();
        state.KeptExerciseRootIdsBySelectionGroupId[MassGroupingTaxonomy.GetResolution(30).Groups[0].Id] = [1000];
        foreach (WorkoutExercisePhase phase in new[] { WorkoutExercisePhase.Warmup, WorkoutExercisePhase.PeakPerformance, WorkoutExercisePhase.Fatigued })
            state.ExerciseScoreAdjustmentsByPhase[phase] = new() { [1000] = 10 };
        if (item.TryGetProperty("phaseFeedback", out var feedback) && feedback.GetBoolean())
            foreach (Exercise exercise in catalog)
            {
                bool first = exercise.Id % 10 == 0;
                state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Warmup][exercise.Id] = first ? 3 : 0;
                state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.PeakPerformance][exercise.Id] = first ? 0 : 3;
                state.ExerciseScoreAdjustmentsByPhase[WorkoutExercisePhase.Fatigued][exercise.Id] = first ? 0 : 3;
            }
        var service = new ExerciseSessionService(catalog, new Random(1), () => DateTimeOffset.Parse("2026-10-31T01:00:00Z"));
        service.StartWorkout(state, item.GetProperty("minutes").GetInt32(), light ? WorkoutModifiers.Light : WorkoutModifiers.None);
        return (service, state, catalog);
    }
}
