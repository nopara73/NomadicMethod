using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutCommandBoundaryTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")), JsonOptions)!;
    private static WorkoutState Copy(WorkoutState state) => JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
    private static ExerciseSessionService Service(Exercise[] catalog) => new(catalog, new Random(4), () => Now);
    private static (Exercise[], ExerciseSessionService, WorkoutState) Checkpoint(string phase)
    {
        Exercise[] catalog = Catalog();
        foreach (Exercise exercise in catalog.Where(WorkoutSequencePolicy.CanUseSingleDirection)) exercise.Score = 10000;
        var service = Service(catalog);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 10, WorkoutModifiers.None);
        WorkoutGroup first = service.GetNextGroup(state)!;
        service.BeginRest(state, first, Now.AddSeconds(15).ToUnixTimeMilliseconds());
        if (first.IsFinalSequenceRound) service.RecordOutcome(state, first, keep: true);
        else service.AdvanceSequence(state, first);
        service.ClearPendingRest(state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        if (phase == "movement")
        {
            service.BeginMovement(state, current, 27000, Now.AddSeconds(27).ToUnixTimeMilliseconds());
            service.PauseMovement(state, current, 27000, true);
        }
        if (phase == "rest") service.BeginRest(state, current, Now.AddSeconds(15).ToUnixTimeMilliseconds());
        return (catalog, service, state);
    }

    [Fact]
    public void ExplicitEndDoesNotDependOnAnIntactUnfinishedPlan()
    {
        var (catalog, _, state) = Checkpoint("ready");
        string work = JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks);
        state.SelectedExerciseIds.Clear();
        state.ActiveSetCountsBySelectionGroupId.Clear();
        Service(catalog).EndActiveWorkout(state);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Equal(work, JsonSerializer.Serialize(Assert.Single(state.WorkoutHistory).Blocks));
    }

    [Fact]
    public void DiscardingAnUncommittedCommandCannotPoisonTheOriginalSession()
    {
        var (catalog, _, original) = Checkpoint("ready");
        var service = Service(catalog);
        WorkoutGroup[] before = service.GetActiveGroups(original).ToArray();
        WorkoutState copy = Copy(original);
        service.EndActiveWorkout(copy);
        Assert.Equal(before, service.GetActiveGroups(original));
        Assert.Equal(10, original.ActiveWorkoutMinutes);
        Assert.Empty(original.WorkoutHistory);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(30)]
    public void EveryCommandCanCrossAWorkerAndPersistenceBoundary(int minutes)
    {
        Exercise[] catalog = Catalog();
        var state = new WorkoutState();
        Service(catalog).Initialize(state);
        Service(catalog).StartWorkout(state, minutes, WorkoutModifiers.None);
        var store = new FakeWorkoutStateStore();
        void Persist()
        {
            var directions = state.ActiveSingleDirectionExerciseIds.ToHashSet();
            int revision = state.ActiveCatalogRevision;
            store.Save(state);
            state = store.Load();
            Assert.Equal(revision, state.ActiveCatalogRevision);
            Assert.True(directions.SetEquals(state.ActiveSingleDirectionExerciseIds));
        }
        Persist();
        WorkoutGroup initial = Service(catalog).GetNextGroup(state)!;
        Assert.True(Service(catalog).CanShuffleNextExercise(state, initial));
        Service(catalog).ShuffleNextExercise(state, initial);
        Persist();
        int completed = 0;
        while (!state.WorkoutCompleted)
        {
            WorkoutGroup group = Service(catalog).GetNextGroup(state)!;
            Assert.NotNull(Service(catalog).GetSelectedExercise(state, group));
            Service(catalog).BeginMovement(state, group, 45000, Now.AddSeconds(45).ToUnixTimeMilliseconds());
            Persist();
            Service(catalog).PauseMovement(state, group, 27000, true);
            Persist();
            Service(catalog).RestoreAfterReopen(state);
            Assert.Equal(27000, Service(catalog).GetPendingMovementMillisecondsRemaining(state, Now.ToUnixTimeMilliseconds()));
            Service(catalog).BeginRest(state, group, Now.AddSeconds(15).ToUnixTimeMilliseconds());
            Persist();
            Service(catalog).PauseRest(state, group, 15000);
            Persist();
            Service(catalog).ResumeRest(state, group, Now.AddSeconds(15).ToUnixTimeMilliseconds());
            Assert.Equal(group.IsFinalSequenceRound, Service(catalog).KeepPendingRest(state));
            if (group.IsFinalSequenceRound) Service(catalog).RecordOutcome(state, group, true);
            else Service(catalog).AdvanceSequence(state, group);
            Service(catalog).ClearPendingRest(state);
            completed++;
            Persist();
        }
        Assert.Equal(minutes, completed);
        WorkoutSessionLog log = Assert.Single(state.WorkoutHistory);
        Assert.Equal(minutes, log.Blocks.Count);
        Assert.Single(log.SelectionChanges);
        Assert.Equal(WorkoutSessionStatus.Completed, log.Status);
        Service(catalog).AcknowledgeCompletion(state);
        Persist();
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Single(state.WorkoutHistory);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void FreshWorkerCanEndEveryCheckpointWithoutReconstructingOrInventingWork(string phase)
    {
        var (catalog, _, original) = Checkpoint(phase);
        WorkoutState state = Copy(original);
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks);
        string feedback = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        string keeps = JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId);
        long id = state.ActiveWorkoutSession.SessionId;
        Service(catalog).EndActiveWorkout(state);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Null(state.ActiveWorkoutSession);
        WorkoutSessionLog ended = Assert.Single(state.WorkoutHistory);
        Assert.Equal(id, ended.SessionId);
        Assert.Equal(WorkoutSessionStatus.Interrupted, ended.Status);
        Assert.Equal(blocks, JsonSerializer.Serialize(ended.Blocks));
        Assert.Equal(feedback, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(keeps, JsonSerializer.Serialize(state.KeptExerciseRootIdsBySelectionGroupId));
        string saved = JsonSerializer.Serialize(state);
        Service(catalog).EndActiveWorkout(state);
        Assert.Equal(saved, JsonSerializer.Serialize(state));
    }

    [Theory]
    [InlineData("ready", 5)]
    [InlineData("movement", 5)]
    [InlineData("rest", 5)]
    [InlineData("ready", 15)]
    [InlineData("movement", 30)]
    [InlineData("rest", 30)]
    public void FreshWorkerCanReadReconfigureResizeAndRestoreFrozenWork(string phase, int minutes)
    {
        var (catalog, service, original) = Checkpoint(phase);
        WorkoutGroup[] before = service.GetActiveGroups(original).ToArray();
        WorkoutGroup current = service.GetNextGroup(original)!;
        WorkoutState state = Copy(original);
        var worker = Service(catalog);
        Assert.Equal(before, worker.GetActiveGroups(state));
        Assert.Equal(current.Id, worker.GetNextGroup(state)!.Id);
        worker.ReconfigureActiveWorkout(state, state.ActiveWorkoutModifiers, current.Id);
        worker.ResizeActiveWorkout(state, minutes);
        Assert.Equal(minutes, worker.GetActiveGroups(state).Count);
        Assert.Equal(before.Take(current.Order), worker.GetActiveGroups(state).Take(current.Order));
        Assert.Equal(original.ActiveWorkoutSession!.SessionId, state.ActiveWorkoutSession!.SessionId);
        Assert.Equal(JsonSerializer.Serialize(original.ActiveWorkoutSession.Blocks), JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks));
        Assert.Single(state.ActiveWorkoutSession.DurationChanges);
        WorkoutState reopened = Copy(state);
        Service(catalog).RestoreAfterReopen(reopened);
        Assert.Equal(worker.GetActiveGroups(state), Service(catalog).GetActiveGroups(reopened));
        if (phase == "movement") Assert.Equal(27000, reopened.PendingMovementMillisecondsRemaining);
    }
}
