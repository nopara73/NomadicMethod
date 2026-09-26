using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutAdaptivePlanningTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private const WorkoutModifiers Profile = WorkoutModifiers.Mirror | WorkoutModifiers.TallMirror |
        WorkoutModifiers.HardFloor | WorkoutModifiers.Wall;
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 4, 0, 0, TimeSpan.Zero);
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)!;
    private static WorkoutState Reload(WorkoutState state) =>
        JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
    private static WorkoutState RejectedState() => new()
    {
        ExerciseScoreAdjustmentsByPhase = new()
        {
            [WorkoutExercisePhase.Warmup] = new() { [130] = -4, [118] = -2, [132] = -2, [1028] = -2, [1031] = -2 },
        },
    };

    public static IEnumerable<object[]> Cases() => JsonSerializer.Deserialize<PlanningCase[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "adaptive-planning-cases.json")),
        JsonOptions)!.SelectMany(item => new[] { 1, 2, 3 }.Select(seed => new object[] { item, seed }));

    [Theory]
    [MemberData(nameof(Cases))]
    public void SharedContractKeepsExactDurationWholeCoverageAndTruthfulFeedback(PlanningCase item, int seed)
    {
        Exercise[] catalog = Catalog();
        foreach (Exercise exercise in catalog) exercise.Score = item.LegacyScore;
        var state = new WorkoutState { ExerciseScoreAdjustmentsByPhase = item.Adjustments };
        var service = new ExerciseSessionService(catalog, new Random(seed), () => Now);
        service.Initialize(state);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        service.StartWorkout(state, item.Minutes, (WorkoutModifiers)item.Modifiers);
        Assert.Equal(item.Minutes, service.GetActiveGroups(state).Count);
        Assert.Equal(item.Minutes, state.ActiveWorkoutSession!.WorkoutMinutes);
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.All(catalog, e => Assert.Equal(item.LegacyScore, e.Score));
        Assert.Empty(state.WorkoutHistory);
        var selections = state.ActiveWorkoutSession.InitialSelections;
        Assert.Equal(MassGroupingTaxonomy.GetResolution(item.ExpectedResolution).Groups.Select(g => g.Id).Order(),
            selections.SelectMany(s => s.CoveredWorkoutGroupIds).Order());
        Assert.Equal(item.ExpectedRejectedBlocks, selections.Where(s => s.SelectionScoreAtStart < 0)
            .Sum(s => s.SequenceBlockCount * s.SetCount));
        Assert.DoesNotContain(selections, s => item.ExcludedRoots.Contains(s.RootExerciseId));
        Assert.Equal(item.Minutes, selections.Sum(s => s.SequenceBlockCount * s.SetCount));
        foreach (var selection in selections)
        {
            Exercise root = catalog.Single(e => e.Id == selection.RootExerciseId);
            Assert.Equal(root.SequenceBlocks.Length, selection.SequenceBlockCount);
            var rounds = service.GetActiveGroups(state).Where(g => g.SelectionKey == selection.SelectionGroupId).ToArray();
            Assert.Equal(selection.SequenceBlockCount * selection.SetCount, rounds.Length);
            Assert.All(rounds, g => Assert.True(WorkoutModifierPolicy.IsCompatible(service.GetSelectedExercise(state, g), (WorkoutModifiers)item.Modifiers)));
        }
    }

    [Fact]
    public void LightAdaptationCannotSpendMoreBlocksOnDemandingWork()
    {
        Exercise[] catalog = Catalog();
        var baseline = new WorkoutState();
        var service = new ExerciseSessionService(catalog, new Random(5), () => Now);
        service.Initialize(baseline);
        service.StartWorkout(baseline, 7, Profile | WorkoutModifiers.Light);
        var state = RejectedState();
        service.Initialize(state);
        service.StartWorkout(state, 7, Profile | WorkoutModifiers.Light);
        int LightBlocks(WorkoutState s) => s.ActiveWorkoutSession!.InitialSelections
            .Where(p => catalog.Single(e => e.Id == p.RootExerciseId).SequenceBlocks
                .All(b => catalog.Single(e => e.Id == b.ExerciseId).MuscularDemand == 0))
            .Sum(p => p.SequenceBlockCount * p.SetCount);
        Assert.True(LightBlocks(state) >= LightBlocks(baseline));
        Assert.True(state.ActiveWorkoutIsLightDay);
        Assert.Equal(7, service.GetActiveGroups(state).Count);
    }

    [Fact]
    public void ExistingReadyWorkoutDoesNotReplanItsGroupsOnUpgrade()
    {
        var service = new ExerciseSessionService(Catalog(), new Random(5), () => Now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 7, Profile);
        state.ExerciseScoreAdjustmentsByPhase = RejectedState().ExerciseScoreAdjustmentsByPhase;
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string session = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(session, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Null(state.ActiveDurationSelectionGroupIds);
    }

    [Fact]
    public void BroaderWorkoutRestoresPausedSidesAndRetainsCompletedWorkThroughEdits()
    {
        Exercise[] catalog = Catalog();
        var service = new ExerciseSessionService(catalog, new Random(5), () => Now);
        var state = RejectedState();
        service.Initialize(state);
        service.StartWorkout(state, 7, Profile);
        Assert.NotNull(state.ActiveDurationSelectionGroupIds);
        WorkoutGroup first = service.GetNextGroup(state)!;
        service.BeginRest(state, first, Now.AddSeconds(15).ToUnixTimeMilliseconds());
        if (first.IsFinalSequenceRound) service.RecordOutcome(state, first, keep: true);
        else service.AdvanceSequence(state, first);
        service.ClearPendingRest(state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        service.BeginMovement(state, current, 25_000, Now.AddSeconds(25).ToUnixTimeMilliseconds());
        service.PauseMovement(state, current, 25_000, pausedByUser: true);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        long sessionId = state.ActiveWorkoutSession.SessionId;
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(25_000, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(current.Id, state.PendingMovementGroupId);
        service.ResizeActiveWorkout(state, 10);
        Assert.Equal(10, service.GetActiveGroups(state).Count);
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        service.ReconfigureActiveWorkout(state, Profile ^ WorkoutModifiers.Mirror ^ WorkoutModifiers.TallMirror, current.Id);
        Assert.Equal(10, service.GetActiveGroups(state).Count);
        Assert.Equal(blocks, JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks));
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(sessionId, state.ActiveWorkoutSession.SessionId);
        Assert.Single(state.ActiveWorkoutSession.DurationChanges);
    }

    public sealed record PlanningCase(string Name, int Minutes, int Modifiers,
        Dictionary<WorkoutExercisePhase, Dictionary<int, int>> Adjustments, int LegacyScore,
        int ExpectedResolution, int ExpectedRejectedBlocks, int[] ExcludedRoots);
}
