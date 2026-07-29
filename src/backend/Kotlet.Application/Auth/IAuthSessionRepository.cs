using Kotlet.Domain.Auth;

namespace Kotlet.Application.Auth;

public interface IAuthSessionRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<RefreshToken?> GetTokenAsync(Guid tokenId, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>
    /// Revokes the given token and every live successor in its replacement chain — one device's
    /// session — leaving the user's sessions on other devices untouched.
    /// </summary>
    Task RevokeChainAsync(RefreshToken token, DateTime revokedAt, CancellationToken cancellationToken);
    void Add(RefreshToken refreshToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
