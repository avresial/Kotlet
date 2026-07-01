using Kotlet.Application.Admin;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Admin;

public sealed class AdminUserRepository(KotletDbContext dbContext) : IAdminUserRepository
{
    public async Task<(IReadOnlyList<User> Users, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.Users.AsNoTracking().Include(user => user.Roles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(user => user.Email.ToLower().Contains(term) ||
                (user.DisplayName != null && user.DisplayName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query.OrderBy(user => user.DisplayName ?? user.Email).ThenBy(user => user.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (users, totalCount);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.Include(user => user.Roles).SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<IReadOnlyDictionary<string, Role>> GetRolesAsync(CancellationToken cancellationToken) =>
        await dbContext.Roles.ToDictionaryAsync(role => role.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);

    public Task<bool> EmailExistsAsync(Guid excludedUserId, string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Id != excludedUserId && user.NormalizedEmail == normalizedEmail, cancellationToken);

    public void Remove(User user) => dbContext.Users.Remove(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
