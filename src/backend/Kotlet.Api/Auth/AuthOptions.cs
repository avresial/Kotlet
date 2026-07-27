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
    /// send the pre-rotation cookie; without this window that benign race revokes every token the
    /// user owns and signs them out everywhere.
    /// </summary>
    public int RefreshReuseGraceSeconds { get; init; } = 30;
}
