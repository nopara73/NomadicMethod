using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using NomadicMethod.Models;
using NomadicMethod.Services;
using Xunit;

namespace NomadicMethod.Tests;

public sealed class WorkoutPreparationQueueTests
{
    [Fact]
    public async Task SupersededRunningPlannerStopsBeforeTheLatestDurationStarts()
    {
        var queue = new WorkoutPreparationQueue();
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var random = new PausedRandom(started, release);
        Exercise[] catalog = Catalog();
        var service = new ExerciseSessionService(catalog, random);
        var savedState = new WorkoutState();
        string savedJson = JsonSerializer.Serialize(savedState);
        var draft = JsonSerializer.Deserialize<WorkoutState>(savedJson)!;
        Task<WorkoutState> stale = queue.RunAsync(() =>
        {
            using (service.ObservePlanningCancellation(cancellation.Token))
                service.PrepareWorkout(draft, 15);
            return draft;
        }, cancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            Task<WorkoutState> latest = queue.RunAsync(() =>
            {
                var replacement = new WorkoutState();
                new ExerciseSessionService(catalog, new Random(1)).PrepareWorkout(replacement, 30);
                return replacement;
            }, CancellationToken.None);
            release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stale);
            Assert.Equal(1, random.Calls);
            Assert.Equal(30, (await latest.WaitAsync(TimeSpan.FromSeconds(15))).ActiveWorkoutMinutes);
            Assert.Equal(savedJson, JsonSerializer.Serialize(savedState));
        }
        finally { release.Set(); }
    }

    [Fact]
    public void CancellingFinishedPreparationDoesNotCancelTheActivatedWorkout()
    {
        var service = new ExerciseSessionService(Catalog(), new Random(1));
        var state = new WorkoutState();
        using var cancellation = new CancellationTokenSource();
        using (service.ObservePlanningCancellation(cancellation.Token))
            service.PrepareWorkout(state, 3);
        cancellation.Cancel();
        service.ActivatePreparedWorkout(state);
        Assert.NotNull(service.GetNextGroup(state));
        Assert.NotNull(state.ActiveWorkoutSession);
    }

    private static Exercise[] Catalog() => JsonSerializer.Deserialize<Exercise[]>(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Assets", "exercises.json")),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() } })!;

    private sealed class PausedRandom(TaskCompletionSource started, ManualResetEventSlim release) : Random(1)
    {
        public int Calls { get; private set; }
        public override int Next(int maxValue)
        {
            Calls++;
            started.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException();
            return base.Next(maxValue);
        }
    }

    [Fact]
    public async Task RapidDurationChangesSkipCancelledPlansAndDiscardTheRunningStaleResult()
    {
        var queue = new WorkoutPreparationQueue();
        var calls = new ConcurrentQueue<int>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var firstCancellation = new CancellationTokenSource();
        using var skippedCancellation = new CancellationTokenSource();
        Task<int> first = queue.RunAsync(() =>
        {
            calls.Enqueue(10);
            started.SetResult();
            if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException();
            return 10;
        }, firstCancellation.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            Task<int> skipped = queue.RunAsync(() => { calls.Enqueue(15); return 15; }, skippedCancellation.Token);
            skippedCancellation.Cancel();
            firstCancellation.Cancel();
            Task<int> latest = queue.RunAsync(() => { calls.Enqueue(90); return 90; }, CancellationToken.None);
            Assert.Equal(new[] { 10 }, calls.ToArray());
            Assert.False(latest.IsCompleted);
            release.Set();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => skipped);
            Assert.Equal(90, await latest.WaitAsync(TimeSpan.FromSeconds(30)));
            Assert.Equal(new[] { 10, 90 }, calls.ToArray());
        }
        finally
        {
            release.Set();
        }
    }

    [Fact]
    public async Task FailedPreparationDoesNotBlockTheNextSelection()
    {
        var queue = new WorkoutPreparationQueue();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queue.RunAsync<int>(
            () => throw new InvalidOperationException("Test preparation failure."), CancellationToken.None));
        Assert.Equal(90, await queue.RunAsync(() => 90, CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30)));
    }
}
