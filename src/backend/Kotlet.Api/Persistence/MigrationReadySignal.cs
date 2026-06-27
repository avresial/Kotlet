namespace Kotlet.Api.Persistence;

/// <summary>
/// Singleton signal that completes when DatabaseMigrationWorker has finished
/// setting up the schema. Tests and other consumers can await WaitAsync() to
/// ensure the database is ready before issuing queries.
/// </summary>
public sealed class MigrationReadySignal
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void SetReady() => _tcs.TrySetResult();

    public Task WaitAsync(CancellationToken cancellationToken = default) =>
        _tcs.Task.WaitAsync(cancellationToken);
}
