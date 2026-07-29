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

    // Tracked on purpose: replay handling rotates the chain's live tail in place.
    public Task<RefreshToken?> GetTokenAsync(Guid tokenId, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens.SingleOrDefaultAsync(token => token.Id == tokenId, cancellationToken);

    public Task<bool> IsMemberAsync(Guid userId, Guid houseId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(
            membership => membership.UserId == userId && membership.HouseId == houseId, cancellationToken);

    public Task<bool> HasHouseAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.HouseMemberships.AnyAsync(membership => membership.UserId == userId, cancellationToken);

    // Walks forward through ReplacedByTokenId. The chain is a singly linked list, so the loop is
    // bounded anyway; the hop cap only guards against data corruption introducing a cycle.
    public async Task RevokeChainAsync(RefreshToken token, DateTime revokedAt, CancellationToken cancellationToken)
    {
        const int maxHops = 64;
        var current = token;
        for (var hop = 0; current is not null && hop < maxHops; hop++)
        {
            current.RevokedAtUtc ??= revokedAt;
            current = current.ReplacedByTokenId is { } nextId
                ? await dbContext.RefreshTokens.SingleOrDefaultAsync(next => next.Id == nextId, cancellationToken)
                : null;
        }
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
