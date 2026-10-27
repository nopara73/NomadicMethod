using System.Collections.Concurrent;
using NomadicMethod.Services;
using Xunit;

namespace NomadicMethod.Tests;

public sealed class WorkoutPreparationQueueTests
{
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
