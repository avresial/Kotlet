namespace Kotlet.Application.Ai;

/// <summary>
/// The provider-agnostic inputs an <see cref="IChatClientFactory"/> needs to build a chat client.
/// Deliberately free of any provider SDK type so the abstraction seam stays in the Application layer:
/// swapping the backing provider changes only the factory implementation, not this contract.
/// </summary>
public sealed record AiChatClientOptions(string? BaseUrl, string ApiKey, string? Model);
