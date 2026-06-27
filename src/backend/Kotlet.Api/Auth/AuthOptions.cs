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
}
