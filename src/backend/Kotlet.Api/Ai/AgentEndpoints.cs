using Kotlet.Api.Auth;
using Kotlet.Application.Ai;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Reflection;

namespace Kotlet.Api.Ai;

public sealed record AgentMessage(string Role, string Content);
public sealed record AgentChatRequest(string Model, IReadOnlyList<AgentMessage> Messages);
public sealed record AgentChatResponse(string Content);

public static class AgentEndpoints
{
    private const string SystemPrompt = "You are Kotlet's kitchen assistant. Use the available Kotlet tools whenever the user asks about their recipes, ingredients, pantry, shopping list, or meal plan. Reply in the user's language.";

    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/agent/chat", Chat).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> Chat(AgentChatRequest request, ICurrentUser user,
        IUserChatClientResolver resolver, IServiceProvider services, CancellationToken ct)
    {
        if (user.UserId is not { } userId) return Results.Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Model) || request.Messages is not { Count: > 0 } or { Count: > 50 })
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["messages"] = ["Choose a model and provide 1 to 50 messages."] });
        if (request.Messages.Any(x => x.Content.Length > 20_000 || x.Role is not ("user" or "assistant")))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["messages"] = ["Messages are invalid or too long."] });

        using var client = await resolver.ResolveAsync(userId, request.Model, ct);
        if (client is null) return Results.NotFound();
        var messages = new List<ChatMessage> { new(ChatRole.System, SystemPrompt) };
        messages.AddRange(request.Messages.Select(x => new ChatMessage(x.Role == "user" ? ChatRole.User : ChatRole.Assistant, x.Content)));
        try
        {
            var response = await client.GetResponseAsync(messages, new ChatOptions { Tools = CreateTools(services) }, ct);
            return Results.Ok(new AgentChatResponse(response.Text ?? ""));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Results.Problem("The AI provider request failed.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static List<AITool> CreateTools(IServiceProvider services) => typeof(AgentEndpoints).Assembly.GetTypes()
        .Where(type => type.GetCustomAttribute<McpServerToolTypeAttribute>() is not null)
        .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance))
        .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() is not null)
        .Select(method => (AITool)AIFunctionFactory.Create(method, target: null, new AIFunctionFactoryOptions
        {
            Name = method.GetCustomAttribute<McpServerToolAttribute>()!.Name ?? method.Name,
            Description = method.GetCustomAttribute<DescriptionAttribute>()?.Description,
            ConfigureParameterBinding = parameter => services.GetService(parameter.ParameterType) is { } service
                ? new() { BindParameter = (_, _) => service, ExcludeFromSchema = true }
                : new()
        })).ToList();
}
