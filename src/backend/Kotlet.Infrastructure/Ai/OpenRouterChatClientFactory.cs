using Kotlet.Application.Ai;
using Microsoft.Extensions.AI;

namespace Kotlet.Infrastructure.Ai;

internal sealed class OpenRouterChatClientFactory : IChatClientFactory
{
    private const string BaseUrl = "https://openrouter.ai/api/v1";
    private const string FallbackModel = "openrouter/free";
    private readonly OpenAiChatClientFactory inner = new();

    public IChatClient Create(AiChatClientOptions options) => inner.Create(options with
    {
        BaseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? BaseUrl : options.BaseUrl,
        Model = string.IsNullOrWhiteSpace(options.Model) ? FallbackModel : options.Model,
    });
}
