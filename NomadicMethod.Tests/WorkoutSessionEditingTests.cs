using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;

namespace NomadicMethod.Tests;

public sealed class WorkoutSessionEditingTests
{
    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() } })!;
    private static WorkoutState Reload(WorkoutState state) =>
        JsonSerializer.Deserialize<WorkoutState>(JsonSerializer.Serialize(state))!;
    private static void Finish(ExerciseSessionService service, WorkoutState state)
    {
        WorkoutGroup group = service.GetNextGroup(state)!;
        service.BeginRest(state, group, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
        if (group.IsFinalSequenceRound) service.RecordOutcome(state, group, keep: true);
        else service.AdvanceSequence(state, group);
        service.ClearPendingRest(state);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void UnfinishedWorkoutResumesTenYearsLaterWithoutNewHistoryOrVotes(string phase)
    {
        Exercise[] catalog = Catalog();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var service = new ExerciseSessionService(catalog, utcNowProvider: () => now);
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 10);
        Finish(service, state);
        WorkoutGroup next = service.GetNextGroup(state)!;
        if (phase == "movement") service.BeginMovement(state, next, 31_000, now.AddSeconds(31).ToUnixTimeMilliseconds());
        if (phase == "rest") service.BeginRest(state, next, now.AddSeconds(15).ToUnixTimeMilliseconds());
        string outcomes = JsonSerializer.Serialize(state.Outcomes);
        string history = JsonSerializer.Serialize(state.WorkoutHistory);
        long id = state.ActiveWorkoutSession!.SessionId;
        state = Reload(state);
        now = now.AddYears(10);
        service.RestoreAfterReopen(state);
        Assert.Equal(id, state.ActiveWorkoutSession!.SessionId);
        Assert.Equal(next.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(outcomes, JsonSerializer.Serialize(state.Outcomes));
        Assert.Equal(history, JsonSerializer.Serialize(state.WorkoutHistory));
        Assert.False(state.WorkoutCompleted);
        if (phase == "movement") {
            Assert.True(state.PendingMovementPausedByUser);
            Assert.Equal(31_000, service.GetPendingMovementMillisecondsRemaining(state, now.ToUnixTimeMilliseconds()));
        }
        if (phase == "rest") Assert.True(state.PendingRestPausedByUser);
    }

    [Theory]
    [InlineData(3, 60)]
    [InlineData(10, 5)]
    [InlineData(30, 45)]
    [InlineData(60, 30)]
    [InlineData(60, 90)]
    [InlineData(90, 15)]
    public void DurationEditsPreserveCompletedAndCurrentBlocksAcrossResolutions(int from, int to)
    {
        var service = new ExerciseSessionService(Catalog());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, from);
        Finish(service, state);
        WorkoutGroup current = service.GetNextGroup(state)!;
        var before = service.GetActiveGroups(state).Take(current.Order).ToArray();
        long id = state.ActiveWorkoutSession!.SessionId;
        service.ResizeActiveWorkout(state, to);
        Assert.Equal(to, service.GetActiveGroups(state).Count);
        Assert.Equal(before, service.GetActiveGroups(state).Take(current.Order));
        Assert.Equal(current.Id, service.GetNextGroup(state)!.Id);
        Assert.Equal(id, state.ActiveWorkoutSession!.SessionId);
        Assert.Single(state.ActiveWorkoutSession.DurationChanges);
        var restored = Reload(state);
        service.RestoreAfterReopen(restored);
        Assert.Equal(current.Id, service.GetNextGroup(restored)!.Id);
        Assert.Equal(to, service.GetActiveGroups(restored).Count);
        while (!restored.WorkoutCompleted) Finish(service, restored);
        Assert.Equal(WorkoutSessionStatus.Completed, restored.WorkoutHistory.Single().Status);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("movement")]
    [InlineData("rest")]
    public void ExplicitEndArchivesOnlyActualWorkAndDoesNotDownvoteCurrentExercise(string phase)
    {
        var service = new ExerciseSessionService(Catalog());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 10);
        Finish(service, state);
        var current = service.GetNextGroup(state)!;
        if (phase == "rest") service.BeginRest(state, current, DateTimeOffset.UtcNow.AddSeconds(15).ToUnixTimeMilliseconds());
        if (phase == "movement") service.BeginMovement(state, current, 25_000, DateTimeOffset.UtcNow.AddSeconds(25).ToUnixTimeMilliseconds());
        string votes = JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase);
        long oldId = state.ActiveWorkoutSession!.SessionId;
        string blocks = JsonSerializer.Serialize(state.ActiveWorkoutSession.Blocks);
        service.EndActiveWorkout(state);
        Assert.Equal(votes, JsonSerializer.Serialize(state.ExerciseScoreAdjustmentsByPhase));
        Assert.Equal(WorkoutSessionStatus.Interrupted, state.WorkoutHistory.Single().Status);
        Assert.Equal(oldId, state.WorkoutHistory.Single().SessionId);
        Assert.Equal(blocks, JsonSerializer.Serialize(state.WorkoutHistory.Single().Blocks));
        Assert.Null(state.ActiveWorkoutSession);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Null(state.PendingRestGroupId);
        Assert.Null(state.PendingMovementGroupId);
        string ended = JsonSerializer.Serialize(state);
        service.EndActiveWorkout(state);
        Assert.Equal(ended, JsonSerializer.Serialize(state));
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(0, state.ActiveWorkoutMinutes);
        Assert.Single(state.WorkoutHistory);
    }

    [Theory]
    [InlineData(WorkoutModifiers.None)]
    [InlineData(WorkoutModifiers.Insect | WorkoutModifiers.Silence | WorkoutModifiers.UpperBodyClothing)]
    [InlineData(WorkoutModifiers.HardFloor | WorkoutModifiers.Silence | WorkoutModifiers.UpperBodyClothing)]
    [InlineData(WorkoutModifiers.Wall | WorkoutModifiers.UpperBodyClothing)]
    public void EditedWorkoutCanChangeModifiersAndDurationAgainDuringPausedWork(WorkoutModifiers profile)
    {
        var service = new ExerciseSessionService(Catalog());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 60, profile);
        for (int i = 0; i < 10; i++) Finish(service, state);
        var current = service.GetNextGroup(state)!;
        service.BeginMovement(state, current, 25_000, DateTimeOffset.UtcNow.AddSeconds(25).ToUnixTimeMilliseconds());
        service.PauseMovement(state, current, 25_000, pausedByUser: true);
        string outcomes = JsonSerializer.Serialize(state.Outcomes);
        string completedWork = JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks);
        service.ResizeActiveWorkout(state, 30);
        Assert.Equal(current.Id, state.PendingMovementGroupId);
        Assert.Equal(25_000, state.PendingMovementMillisecondsRemaining);
        Assert.Equal(outcomes, JsonSerializer.Serialize(state.Outcomes));
        service.ReconfigureActiveWorkout(state, profile ^ WorkoutModifiers.Wall, current.Id);
        Assert.Equal(completedWork, JsonSerializer.Serialize(state.ActiveWorkoutSession!.Blocks));
        outcomes = JsonSerializer.Serialize(state.Outcomes);
        service.ResizeActiveWorkout(state, 45);
        state = Reload(state);
        service.RestoreAfterReopen(state);
        Assert.Equal(45, service.GetActiveGroups(state).Count);
        Assert.Equal(outcomes, JsonSerializer.Serialize(state.Outcomes));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(90)]
    public void ExtensionAtLastFineMuscleSlotStillPreservesAllCompletedSlots(int minutes)
    {
        var service = new ExerciseSessionService(Catalog());
        var state = new WorkoutState();
        service.Initialize(state);
        service.StartWorkout(state, 30);
        while (service.GetNextGroup(state)!.Order < 30) Finish(service, state);
        string outcomes = JsonSerializer.Serialize(state.Outcomes);
        service.ResizeActiveWorkout(state, minutes);
        Assert.Equal(minutes, service.GetActiveGroups(state).Count);
        Assert.Equal(outcomes, JsonSerializer.Serialize(state.Outcomes));
        Assert.Equal(30, service.GetNextGroup(state)!.Order);
        string beforeInvalidChange = JsonSerializer.Serialize(state);
        Assert.Throws<InvalidOperationException>(() => service.ResizeActiveWorkout(state, 3));
        Assert.Equal(beforeInvalidChange, JsonSerializer.Serialize(state));
    }

    [Fact]
    public void UnsupportedDurationPlanLeavesTheEntireActiveStateUntouched()
    {
        var service = new ExerciseSessionService(Catalog(), new Random(0));
        var state = new WorkoutState();
        var profile = WorkoutModifiers.Insect | WorkoutModifiers.HardFloor |
            WorkoutModifiers.Silence | WorkoutModifiers.UpperBodyClothing;
        service.Initialize(state);
        service.StartWorkout(state, 60, profile);
        for (int i = 0; i < 10; i++) Finish(service, state);
        service.ResizeActiveWorkout(state, 30);
        service.ReconfigureActiveWorkout(state, profile | WorkoutModifiers.Wall, service.GetNextGroup(state)!.Id);
        string before = JsonSerializer.Serialize(state);
        // The catalog has no hard-floor Insect hand movement for this newly
        // available finer Wall slot. A failed plan must not discard progress.
        Assert.Throws<InvalidOperationException>(() => service.ResizeActiveWorkout(state, 45));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }
}
