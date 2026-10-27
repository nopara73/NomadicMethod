namespace NomadicMethod.Services;

public sealed class WorkoutPreparationQueue
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<T> RunAsync<T>(Func<T> prepare, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepare);
        // Planning is synchronous. Cancelled requests waiting for the current
        // calculation must never compete with its replacement for CPU time.
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                T result = prepare();
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
