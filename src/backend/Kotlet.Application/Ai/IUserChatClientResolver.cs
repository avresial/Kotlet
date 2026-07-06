using Microsoft.Extensions.AI;

namespace Kotlet.Application.Ai;

/// <summary>
/// Resolves an <see cref="IChatClient"/> from a user's stored AI provider configuration.
/// Returns <see langword="null"/> when the user has no enabled, fully configured provider.
/// The caller owns the returned client and must dispose it.
/// </summary>
public interface IUserChatClientResolver
{
    Task<IChatClient?> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}
