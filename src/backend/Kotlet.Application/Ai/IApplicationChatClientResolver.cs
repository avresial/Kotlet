using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>
/// Builds an <see cref="IChatClient"/> from the application-level AI credentials
/// (<see cref="ApplicationAiOptions"/>). Returns <see langword="null"/> when no application API key is
/// configured. Unlike <see cref="IUserChatClientResolver"/>, resolution does not depend on any user.
/// The caller owns the returned client and must dispose it.
/// </summary>
public interface IApplicationChatClientResolver
{
    IChatClient? Resolve();
}
