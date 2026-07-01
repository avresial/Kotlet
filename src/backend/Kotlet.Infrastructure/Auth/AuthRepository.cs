using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Auth;

public sealed class AuthRepository(KotletDbContext dbContext) : IAuthRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.Include(user => user.Roles).SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<User?> GetUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.Include(user => user.Roles)
            .SingleOrDefaultAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<Role> GetRoleAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Roles.SingleAsync(role => role.Name == name, cancellationToken);

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.HouseId == houseId, cancellationToken);

    public Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(membership => membership.UserId == userId, cancellationToken);

    public Task<Guid?> GetFirstHouseIdAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.Where(membership => membership.UserId == userId)
            .OrderBy(membership => membership.JoinedAtUtc)
            .Select(membership => (Guid?)membership.HouseId)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<RefreshSession?> GetRefreshSessionAsync(
        string tokenHash, DateTime now, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.AsNoTracking()
            .Where(token => token.TokenHash == tokenHash && token.RevokedAtUtc == null && token.ExpiresAtUtc > now)
            .Select(token => new RefreshSession(token.UserId, token.HouseId, token.User.Email,
                token.User.DisplayName, token.User.Roles.Select(role => role.Name).ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

    public void Add(User user) => dbContext.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
