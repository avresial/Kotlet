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
            Id = Guid.NewGuid(),
            UserId = user.Id,
            HouseId = activeHouseId,
            TokenHash = Hash(raw),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_auth.RefreshTokenDays),
            CreatedByIp = context.Connection.RemoteIpAddress?.ToString(),
            UserAgent = context.Request.Headers.UserAgent.ToString() is { Length: > 0 } value ? value[..Math.Min(value.Length, 512)] : null
        });
    }

    /// <summary>
    /// Validates a Kotlet access token and returns its principal, or <c>null</c> when the
    /// token is missing, malformed, expired or fails signature/issuer/audience checks. Used by
    /// the OAuth session bridge, which cannot supply the token through the Authorization header
    /// because it is reached by a top-level browser navigation.
    /// </summary>
    public ClaimsPrincipal? ValidateAccessToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwt.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return null;
        }
    }

    public string Hash(string rawToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    public void SetRefreshCookie(HttpResponse response, string rawToken, DateTime expiresAtUtc, bool secure) =>
        response.Cookies.Append(_auth.RefreshTokenCookieName, rawToken, CookieOptions(expiresAtUtc, secure));

    public void ClearRefreshCookie(HttpResponse response, bool secure) =>
        response.Cookies.Delete(_auth.RefreshTokenCookieName, CookieOptions(DateTime.UnixEpoch, secure));

    public string? ReadRefreshCookie(HttpRequest request) => request.Cookies[_auth.RefreshTokenCookieName];

    /// <summary>
    /// Reads the refresh token from the cookie, falling back to the <c>X-Refresh-Token</c> header.
    /// The SPA is served from a different site (GitHub Pages) than the API, so the refresh cookie
    /// is a third-party cookie that many mobile browsers refuse to store; those clients keep the
    /// token themselves and present it in the header instead.
    /// </summary>
    public string? ReadRefreshToken(HttpRequest request) =>
        ReadRefreshCookie(request) is { Length: > 0 } cookie
            ? cookie
            : request.Headers[RefreshTokenHeaderName].ToString() is { Length: > 0 } header ? header : null;

    public const string RefreshTokenHeaderName = "X-Refresh-Token";

    private static CookieOptions CookieOptions(DateTime expires, bool secure) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = secure ? SameSiteMode.None : SameSiteMode.Lax,
        Expires = expires,
        // Broadened from "/api/auth" so house switching (under /api/houses) can read and update the
        // active-home pointer on the live refresh token, keeping a switch sticky across silent refreshes.
        Path = "/"
    };
}
