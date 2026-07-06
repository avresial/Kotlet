using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>
/// Resolves the application-level chat client from <see cref="ApplicationAiOptions"/> through the
/// shared <see cref="IChatClientFactory"/>. The credentials are static configuration, so this can be a
/// singleton — no per-request lookup is needed as it is for the per-user resolver.
/// </summary>
internal sealed class ApplicationChatClientResolver(
    ApplicationAiOptions options, IChatClientFactory factory) : IApplicationChatClientResolver
{
    public IChatClient? Resolve() =>
        options.IsConfigured
            ? factory.Create(new AiChatClientOptions(options.BaseUrl, options.ApiKey!, options.Model))
            : null;
}
