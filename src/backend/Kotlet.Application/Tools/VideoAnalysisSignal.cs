using System.Threading.Channels;

namespace Kotlet.Application.Tools;

public interface IVideoAnalysisSignal
{
    void Enqueue(Guid jobId);
    ValueTask<Guid> WaitAsync(CancellationToken cancellationToken);
}

internal sealed class VideoAnalysisSignal : IVideoAnalysisSignal
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);

    public ValueTask<Guid> WaitAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
