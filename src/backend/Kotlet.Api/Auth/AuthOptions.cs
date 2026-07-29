namespace Kotlet.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SigningKey { get; init; }
    public int AccessTokenMinutes { get; init; } = 15;
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public int RefreshTokenDays { get; init; } = 30;
    public string RefreshTokenCookieName { get; init; } = "kotlet_refresh";

    /// <summary>
    /// How long after a refresh token has been rotated it may still be presented without being
    /// treated as theft. Two tabs waking up together, or a request retried after a timeout, both
    /// send the pre-rotation token; without this window that benign race kills the session. Sized
    /// to outlast a cold start of the API's sleep-when-idle hosting plan, during which a refresh
    /// can succeed server-side long after the client gave up on the response.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; init; } = 120;
}
