using Kotlet.Api.Auth;
using Kotlet.Application.Auth;

namespace Kotlet.Api.Houses;

public sealed class HouseSessionService(IAuthSessionRepository sessions, TokenService tokens)
{
    public async Task<TokenResponse?> ActivateAsync(
        Guid userId, Guid? houseId, HttpContext context, CancellationToken cancellationToken)
    {
        var user = await sessions.GetUserAsync(userId, cancellationToken);
        if (user is null) return null;

        var rawRefreshToken = tokens.ReadRefreshCookie(context.Request);
        if (!string.IsNullOrEmpty(rawRefreshToken))
        {
            var refreshToken = await sessions.GetRefreshTokenAsync(tokens.Hash(rawRefreshToken), cancellationToken);
            if (refreshToken is { RevokedAtUtc: null })
            {
                refreshToken.HouseId = houseId;
                await sessions.SaveChangesAsync(cancellationToken);
            }
        }

        var accessToken = tokens.CreateAccessToken(user, houseId);
        return new(accessToken.Token, accessToken.ExpiresAtUtc);
    }
}
