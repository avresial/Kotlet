using System.Diagnostics;

namespace Kotlet.Bench;

/// <summary>
/// Counts EF Core commands by subscribing to the framework's DiagnosticSource, so the
/// measured application keeps its production service wiring. Counting is process-wide;
/// the benchmark issues MCP calls sequentially, so a count taken around one call belongs
/// to that call.
/// </summary>
public sealed class DbQueryCounter : IObserver<DiagnosticListener>, IDisposable
{
    private const string ListenerName = "Microsoft.EntityFrameworkCore";
    private const string CommandExecuted = "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted";

    private readonly List<IDisposable> subscriptions = [];
    private int count;

    private DbQueryCounter() { }

    public static DbQueryCounter Start()
    {
        var counter = new DbQueryCounter();
        counter.subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(counter));
        return counter;
    }

    /// <summary>
    /// Commands executed so far. Callers take the difference across a call to attribute
    /// queries to it.
    /// </summary>
    public int Count => Volatile.Read(ref count);

    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener)
    {
        if (listener.Name == ListenerName)
            subscriptions.Add(listener.Subscribe(new CommandObserver(this)));
    }

    void IObserver<DiagnosticListener>.OnCompleted() { }
    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    public void Dispose()
    {
        foreach (var subscription in subscriptions)
            subscription.Dispose();
        subscriptions.Clear();
    }

    private sealed class CommandObserver(DbQueryCounter owner) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            if (value.Key == CommandExecuted)
                Interlocked.Increment(ref owner.count);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }
    }
}
