using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class SingleDirectionTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 4, 0, 0, TimeSpan.Zero);
    private static Contract Cases => JsonSerializer.Deserialize<Contract>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "single-direction-cases.json")), JsonOptions)!;
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)!;
    private static WorkoutState Reload(WorkoutState state) =>
        JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
    private static (ExerciseSessionService Service, WorkoutState State) Start(int minutes)
    {
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == Cases.PreferredRoot).Score = 10000;
        var service = new ExerciseSessionService(catalog, new Random(1), () => Now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, minutes, WorkoutModifiers.None);
        return (service, state);
    }
    private static void Finish(ExerciseSessionService service, WorkoutState state)
    {
        WorkoutGroup round = service.GetNextGroup(state)!;
        service.BeginRest(state, round, Now.AddSeconds(15).ToUnixTimeMilliseconds());
        if (round.IsFinalSequenceRound) service.RecordOutcome(state, round, keep: true);
        else service.AdvanceSequence(state, round);
        service.ClearPendingRest(state);
    }

    private static void AssertCompleteSides(ExerciseSessionService service, WorkoutState state)
    {
        foreach (var selection in service.GetActiveGroups(state).GroupBy(g => g.SelectionKey))
        {
            Exercise root = service.GetSelectedExercise(state, selection.First());
            if (!root.SequenceBlocks.Any(b => b.SideCue != ExerciseSequenceSideCue.None)) continue;
            Assert.Equal(root.SequenceBlocks.Length * selection.First().SetCount, selection.Count());
            Assert.Equal(root.SequenceBlocks.Select(b => b.SideCue),
                selection.Where(g => g.SetNumber == 1).Select(g => g.SequenceSideCue));
        }
    }

    [Theory]
    [InlineData(30, 3)]
    [InlineData(30, 5)]
    [InlineData(30, 7)]
    [InlineData(30, 10)]
    [InlineData(30, 15)]
    [InlineData(30, 20)]
    [InlineData(10, 30)]
    public void IndependentPreparedWorkoutUsesItsOwnFrozenSequences(int previousMinutes, int minutes)
    {
        var (service, state) = Start(previousMinutes);
        while (service.GetNextGroup(state) is not null) Finish(service, state);
        service.AcknowledgeCompletion(state);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        WorkoutState preparedState = Reload(state);
        Exercise[] catalog = Catalog();
        catalog.Single(e => e.Id == Cases.PreferredRoot).Score = 10000;
        var preparationService = new ExerciseSessionService(catalog, new Random(1), () => Now.AddDays(1));
        preparationService.PrepareWorkout(preparedState, minutes, WorkoutModifiers.None);
        string keeps = JsonSerializer.Serialize(preparedState.KeptExerciseRootIdsBySelectionGroupId);
        WorkoutGroup[] preparedRounds = preparationService.GetActiveGroups(preparedState).ToArray();
        string selections = JsonSerializer.Serialize(preparedState.SelectedExerciseIds);

        service.ActivatePreparedWorkout(preparedState);

        Assert.Equal(preparedRounds, service.GetActiveGroups(preparedState));
        Assert.Equal(minutes, preparedRounds.Length);
        Assert.Equal(selections, JsonSerializer.Serialize(preparedState.SelectedExerciseIds));
        Assert.Equal(history, JsonSerializer.Serialize(preparedState.WorkoutHistory));
        Assert.Equal(keeps, JsonSerializer.Serialize(preparedState.KeptExerciseRootIdsBySelectionGroupId));
        Assert.Empty(preparedState.ActiveWorkoutSession!.Blocks);
        Assert.Equal(minutes, preparedState.ActiveWorkoutSession.InitialSelections.Sum(s => s.SequenceBlockCount * s.SetCount));
        AssertCompleteSides(service, preparedState);
        WorkoutState reopened = Reload(preparedState);
        service.RestoreAfterReopen(reopened);
        Assert.Equal(preparedRounds, service.GetActiveGroups(reopened));
    }

    [Fact]
    public void SharedDirectionsKeepTheirIdentityAndNeverSplitSides()
    {
        Exercise[] catalog = Catalog();
        Assert.All(Cases.DirectionalRoots, id => Assert.True(
            WorkoutSequencePolicy.CanUseSingleDirection(catalog.Single(e => e.Id == id))));
        foreach (int minutes in Cases.ShortMinutes.Concat(Cases.CompleteMinutes))
            foreach (int id in Cases.DirectionalRoots)
                Assert.Equal(minutes < 30 ? 1 : 2,
                    WorkoutSequencePolicy.GetBlockCount(catalog.Single(e => e.Id == id), minutes));
        Exercise[] paired = catalog.Where(e => e.SequenceBlocks.Any(b => b.SideCue != ExerciseSequenceSideCue.None)).ToArray();
        Assert.NotEmpty(paired);
        Assert.DoesNotContain(paired, WorkoutSequencePolicy.CanUseSingleDirection);
        foreach (Exercise exercise in paired)
            foreach (int minutes in Cases.ShortMinutes)
                Assert.Equal(exercise.SequenceBlocks.Length,
                    WorkoutSequencePolicy.GetBlockCount(exercise, minutes));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(20)]
    public void ShortWorkoutSelectsOneDirectionAndKeepsSidesComplete(int minutes)
    {
        var (service, state) = Start(minutes);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        Assert.Equal(minutes, rounds.Length);
        WorkoutGroup selected = Assert.Single(rounds.DistinctBy(g => g.SelectionKey), g => service.GetSelectedExercise(state, g).Id == Cases.PreferredRoot);
        Assert.Equal(1, selected.SequenceBlockCount);
        Assert.Equal(Cases.DirectionCue, selected.SequenceDirectionCue);
        Assert.Equal(Cases.MediaSegment, selected.SequenceMediaSegment);
        AssertCompleteSides(service, state);
        Assert.Equal(10000, service.GetSelectedExercise(state, selected).Score);
        service.ShuffleNextExercise(state, service.GetNextGroup(state)!);
        service.ReconfigureActiveWorkout(state, WorkoutModifiers.Silence, service.GetNextGroup(state)!.Id);
        Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        AssertCompleteSides(service, state);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void SingleDirectionReopensWithOriginalProgressAndTruthfulFeedback(string phase)
    {
        var (service, state) = Start(5);
        while (service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id != Cases.PreferredRoot) Finish(service, state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        if (phase == "movement") service.BeginMovement(state, current, 27000, Now.AddSeconds(27).ToUnixTimeMilliseconds());
        if (phase == "rest") service.BeginRest(state, current, Now.AddSeconds(15).ToUnixTimeMilliseconds());
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        string log = JsonSerializer.Serialize(state.ActiveWorkoutSession);
        string outcomes = JsonSerializer.Serialize(state.Outcomes);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(log, JsonSerializer.Serialize(state.ActiveWorkoutSession));
        Assert.Equal(outcomes, JsonSerializer.Serialize(state.Outcomes));
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        if (phase == "movement") Assert.Equal(27000, state.PendingMovementMillisecondsRemaining);
        Finish(service, state);
        WorkoutBlockLog block = Assert.Single(state.ActiveWorkoutSession!.Blocks, b => b.ExerciseId == Cases.PreferredRoot);
        Assert.Equal(Cases.DirectionCue, block.DirectionCue);
        Assert.Equal(10000, service.GetSelectedExercise(state, current).Score);
    }

    [Fact]
    public void DurationEditKeepsCurrentPairAndUsesSingleDirectionsForRemainingWork()
    {
        var (service, state) = Start(45);
        while (service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id != Cases.PreferredRoot) Finish(service, state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        WorkoutGroup[] prefix = service.GetActiveGroups(state)
            .Where(g => g.Order <= current.Order || g.SelectionKey == current.SelectionKey).ToArray();
        service.ResizeActiveWorkout(state, 20);
        Assert.Equal(prefix, service.GetActiveGroups(state).Take(prefix.Length));
        Assert.Equal(2, service.GetSelectedExercise(state, current).SequenceBlocks.Length);
        Assert.DoesNotContain(Cases.PreferredRoot, state.ActiveSingleDirectionExerciseIds);
        Assert.Contains(409, state.ActiveSingleDirectionExerciseIds);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(20, service.GetActiveGroups(state).Count);
        Assert.Equal(2, service.GetSelectedExercise(state, service.GetNextGroup(state)!).SequenceBlocks.Length);
    }

    [Fact]
    public void OlderUnfinishedWorkoutRetainsBothDirections()
    {
        var (service, state) = Start(45);
        WorkoutGroup[] rounds = service.GetActiveGroups(state).ToArray();
        state.ActiveSingleDirectionExerciseIds.Clear();
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(rounds, service.GetActiveGroups(state));
        Assert.Equal(2, WorkoutCatalogPolicy.ForWorkout(Catalog(), 82)
            .Single(e => e.Id == Cases.PreferredRoot).SequenceBlocks.Length);
    }

    [Fact]
    public void SpareShortWorkoutCapacityRetainsCompleteSidePairs()
    {
        Exercise[] catalog = Catalog();
        int[] sidedIds = catalog.Where(e => e.SequenceBlocks.Any(b => b.SideCue != ExerciseSequenceSideCue.None))
            .Select(e => e.Id).ToArray();
        foreach (Exercise exercise in catalog.Where(e => sidedIds.Contains(e.Id))) exercise.Score = 10000;
        var service = new ExerciseSessionService(catalog, new Random(1), () => Now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 10, WorkoutModifiers.Insect | WorkoutModifiers.Shy);
        Assert.True(state.ActiveWorkoutSession!.InitialSelections.Count < 10);
        Assert.Contains(state.ActiveWorkoutSession.InitialSelections, s => sidedIds.Contains(s.RootExerciseId));
        Assert.Equal(10, service.GetActiveGroups(state).Count);
        AssertCompleteSides(service, state);
    }

    [Fact]
    public void ExtendingWorkoutRetainsCurrentSingleDirectionAndRestoresPairsElsewhere()
    {
        var (service, state) = Start(5);
        while (service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id != Cases.PreferredRoot) Finish(service, state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        service.ResizeActiveWorkout(state, 45);
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Contains(Cases.PreferredRoot, state.ActiveSingleDirectionExerciseIds);
        Assert.DoesNotContain(409, state.ActiveSingleDirectionExerciseIds);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Single(service.GetSelectedExercise(state, service.GetNextGroup(state)!).SequenceBlocks);
        Assert.Equal(2, WorkoutCatalogPolicy.ForWorkout(Catalog(), 82, state.ActiveSingleDirectionExerciseIds)
            .Single(e => e.Id == 409).SequenceBlocks.Length);
    }

    private sealed class Contract
    {
        public required int[] ShortMinutes { get; init; }
        public required int[] CompleteMinutes { get; init; }
        public required int[] DirectionalRoots { get; init; }
        public int PreferredRoot { get; init; }
        public ExerciseSequenceDirectionCue DirectionCue { get; init; }
        public ExerciseSequenceMediaSegment MediaSegment { get; init; }
    }

    [Fact]
    public void ShorteningAndReopeningPreservesTheWholeCurrentSidePair()
    {
        Exercise[] catalog = Catalog();
        foreach (Exercise exercise in catalog.Where(e => e.SequenceBlocks.Any(b => b.SideCue != ExerciseSequenceSideCue.None)))
            exercise.Score = 10000;
        var service = new ExerciseSessionService(catalog, new Random(1), () => Now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 45, WorkoutModifiers.None);
        WorkoutGroup target = service.GetActiveGroups(state).First(g => g.SequenceSideCue != ExerciseSequenceSideCue.None &&
            g.Order <= 13 && g.SequenceBlockCount == 2 && g.SetCount == 1);
        while (service.GetNextGroup(state)!.Id != target.Id) Finish(service, state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        int pairedRoot = service.GetSelectedExercise(state, current).Id;
        WorkoutGroup[] pair = service.GetActiveGroups(state).Where(g => g.SelectionKey == current.SelectionKey).ToArray();
        WorkoutGroup[] prefix = service.GetActiveGroups(state).Where(g =>
            g.Order <= current.Order || g.SelectionKey == current.SelectionKey).ToArray();
        Assert.Equal(2, pair.Length);
        service.BeginMovement(state, current, 27000, Now.AddSeconds(27).ToUnixTimeMilliseconds());
        service.ResizeActiveWorkout(state, 15);
        Assert.Equal(prefix, service.GetActiveGroups(state).Take(prefix.Length));
        Assert.Equal(pairedRoot, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(pairedRoot, service.GetSelectedExercise(state, service.GetNextGroup(state)!).Id);
        Assert.Equal(27000, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(2, service.GetActiveGroups(state).Count(g => g.SelectionKey == current.SelectionKey));
    }
}
