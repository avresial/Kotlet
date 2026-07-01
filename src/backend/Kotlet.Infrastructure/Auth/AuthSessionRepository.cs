using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Infrastructure.Auth;

public sealed class AuthSessionRepository(KotletDbContext dbContext) : IAuthSessionRepository
{
    public Task<User?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Users.Include(user => user.Roles).SingleOrDefaultAsync(user => user.Id == userId, cancellationToken);

    public Task<RefreshToken?> GetRefreshTokenAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.Include(token => token.User).ThenInclude(user => user.Roles)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

    public Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.HouseId == houseId, cancellationToken);

    public Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(membership => membership.UserId == userId, cancellationToken);

    public async Task RevokeFamilyAsync(Guid userId, DateTime revokedAt, CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        tokens.ForEach(token => token.RevokedAtUtc = revokedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Add(RefreshToken refreshToken) => dbContext.RefreshTokens.Add(refreshToken);

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
