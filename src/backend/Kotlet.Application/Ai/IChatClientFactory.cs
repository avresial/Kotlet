using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>
/// Builds a configured <see cref="IChatClient"/> pipeline for a given provider configuration.
/// The concrete implementation — provider SDK and middleware pipeline (function invocation,
/// telemetry) — lives in the Infrastructure layer. Application code depends only on this seam
/// and on <see cref="IChatClient"/>, never on a provider SDK type.
/// </summary>
public interface IChatClientFactory
{
    IChatClient Create(AiChatClientOptions options);
}
