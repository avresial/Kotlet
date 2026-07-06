using System.Threading.Channels;

namespace Kotlet.Application.Ingredients;

/// <summary>
/// Channel-backed <see cref="IIngredientTranslationSignal"/>. The channel has capacity one and drops
/// writes when full, so any number of <see cref="Notify"/> calls while a pass is pending coalesce into
/// exactly one queued wake-up. Registered as a singleton so the producing request scope and the
/// long-lived worker share the same instance.
/// </summary>
internal sealed class IngredientTranslationSignal : IIngredientTranslationSignal
{
    private readonly Channel<byte> _channel = Channel.CreateBounded<byte>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify() => _channel.Writer.TryWrite(0);

    public async ValueTask WaitAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
