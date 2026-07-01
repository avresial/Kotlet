using Kotlet.Domain.Auth;

namespace Kotlet.Application.Admin;

public interface IAdminUserRepository
{
    Task<(IReadOnlyList<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize, string? search, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<string, Role>> GetRolesAsync(CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid excludedUserId, string normalizedEmail, CancellationToken cancellationToken);
    void Remove(User user);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
