using System.ClientModel;
using Kotlet.Application.Ai;
using Microsoft.Extensions.AI;
using OpenAI;

namespace Kotlet.Infrastructure.Ai;

/// <summary>
/// Builds an <see cref="IChatClient"/> over an OpenAI-compatible endpoint. This is the single place
/// that references the OpenAI SDK; swapping providers (Azure OpenAI, Anthropic, a local gateway)
/// means replacing this class, not the Application services that depend on <see cref="IChatClientFactory"/>.
/// </summary>
internal sealed class OpenAiChatClientFactory : IChatClientFactory
{
    private const string FallbackModel = "gpt-4o-mini";

    public IChatClient Create(AiChatClientOptions options)
    {
        var clientOptions = new OpenAIClientOptions();
        if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            clientOptions.Endpoint = new Uri(options.BaseUrl);

        var model = string.IsNullOrWhiteSpace(options.Model) ? FallbackModel : options.Model;
        var chatClient = new OpenAIClient(new ApiKeyCredential(options.ApiKey), clientOptions)
            .GetChatClient(model)
            .AsIChatClient();

        // Function invocation is a no-op for the tool-less translation feature but keeps the pipeline
        // ready for the chat feature; OpenTelemetry routes AI spans into the existing telemetry stack.
        // A new client is built per request today; caching by configuration is a future optimization.
        return new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .UseOpenTelemetry()
            .Build();
    }
}
