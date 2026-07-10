using System.Threading.Channels;

namespace Kotlet.Application.Recipes;

public interface IRecipeImportSignal
{
    void Enqueue(Guid jobId);
    ValueTask<Guid> WaitAsync(CancellationToken cancellationToken);
}

internal sealed class RecipeImportSignal : IRecipeImportSignal
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>();

    public void Enqueue(Guid jobId) => channel.Writer.TryWrite(jobId);
    public ValueTask<Guid> WaitAsync(CancellationToken cancellationToken) =>
        channel.Reader.ReadAsync(cancellationToken);
}
