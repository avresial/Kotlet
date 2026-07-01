using Kotlet.Domain.Auth;

namespace Kotlet.Application.Auth;

public interface IAuthRepository
{
    Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<User?> GetUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<Role> GetRoleAsync(string name, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken);
    Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> GetFirstHouseIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<RefreshSession?> GetRefreshSessionAsync(string tokenHash, DateTime now, CancellationToken cancellationToken);
    void Add(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserPasswordService
{
    string Hash(User user, string password);
    bool Verify(User user, string passwordHash, string password);
}
