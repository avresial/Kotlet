using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kotlet.Domain.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kotlet.Api.Auth;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions, IOptions<AuthOptions> authOptions)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly AuthOptions _auth = authOptions.Value;

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user, Guid? activeHouseId)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role.Name)));
        if (activeHouseId is { } houseId) claims.Add(new Claim(KotletClaimTypes.HouseId, houseId.ToString()));
        var token = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims,
            now, expires, new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string RawToken, RefreshToken Entity) CreateRefreshToken(User user, HttpContext context, Guid? activeHouseId)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var now = DateTime.UtcNow;
        return (raw, new RefreshToken
        {
            Id = Guid.NewGuid(), UserId = user.Id, HouseId = activeHouseId, TokenHash = Hash(raw), CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_auth.RefreshTokenDays),
            CreatedByIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString() is { Length: > 0 } value ? value[..Math.Min(value.Length, 512)] : null
        });
    }

    public string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public void SetRefreshCookie(HttpResponse response, string rawToken, DateTime expiresAtUtc, bool secure) =>
        response.Cookies.Append(_auth.RefreshTokenCookieName, rawToken, CookieOptions(expiresAtUtc, secure));

    public void ClearRefreshCookie(HttpResponse response, bool secure) =>
        response.Cookies.Delete(_auth.RefreshTokenCookieName, CookieOptions(DateTime.UnixEpoch, secure));

    public string? ReadRefreshCookie(HttpRequest request) => request.Cookies[_auth.RefreshTokenCookieName];

    private static CookieOptions CookieOptions(DateTime expires, bool secure) => new()
    {
        HttpOnly = true, Secure = secure, SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax, Expires = expires,
        // Broadened from "/api/auth" so house switching (under /api/houses) can read and update the
        // active-home pointer on the live refresh token, keeping a switch sticky across silent refreshes.
        Path = "/"
    };
}
