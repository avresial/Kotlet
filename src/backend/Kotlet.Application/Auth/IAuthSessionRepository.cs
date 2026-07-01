using Kotlet.Domain.Auth;

namespace Kotlet.Application.Auth;

public interface IAuthSessionRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken);
    Task RevokeFamilyAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken);
    void Add(RefreshToken refreshToken);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
