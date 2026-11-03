using Kotlet.Application.Auth;
using Kotlet.Domain.Auth;
using Microsoft.Extensions.Options;

namespace Kotlet.Api.Auth;

/// <summary>
/// Owns the browser session lifecycle: issuing, rotating and revoking refresh tokens, and keeping
/// the refresh cookie in step. Lives in the API layer because token creation and the cookie are
/// bound to the HTTP request.
/// </summary>
public sealed class AuthSessionService(
    IAuthSessionRepository sessions,
    TokenService tokens,
    IOptions<AuthOptions> authOptions,
    IWebHostEnvironment environment)
{
    // How far Replay follows the replacement chain. A handful of rotations can legitimately pile up
    // inside the grace window; anything longer is not a straggler and is left to reuse detection.
    private const int MaxReplayChainHops = 8;

    private bool IsSecure => !environment.IsDevelopment() && !environment.IsEnvironment("Test");

    public async Task<AuthResponse> SignInAsync(
        User user, Guid? activeHouseId, bool hasHouse, HttpContext context, CancellationToken cancellationToken)
    {
        var raw = await IssueAsync(user, activeHouseId, context, cancellationToken);
        return Response(user, activeHouseId, hasHouse, raw);
    }

    /// <summary>
    /// Signs in a just-registered user. Returns null when saving the session fails because a
    /// concurrent registration claimed the same account.
    /// </summary>
    public async Task<AuthResponse?> TrySignInNewUserAsync(
        User user, HttpContext context, CancellationToken cancellationToken)
    {
        var (raw, refreshToken) = tokens.CreateRefreshToken(user, context, null);
        sessions.Add(refreshToken);
        if (!await sessions.TrySaveChangesAsync(cancellationToken))
        {
            return null;
        }
        tokens.SetRefreshCookie(context.Response, raw, refreshToken.ExpiresAtUtc, IsSecure);
        return Response(user, null, hasHouse: false, raw);
    }

    /// <summary>Starts a cookie session for an existing user; false when the user no longer exists.</summary>
    public async Task<bool> StartSessionAsync(
        Guid userId, Guid? activeHouseId, HttpContext context, CancellationToken cancellationToken)
    {
        var user = await sessions.GetUserAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        await IssueAsync(user, activeHouseId, context, cancellationToken);
        return true;
    }

    /// <summary>
    /// Rotates the presented refresh token. Returns null, with the refresh cookie cleared, when the
    /// token is missing, expired, or reused outside the grace window.
    /// </summary>
    public async Task<AuthResponse?> RefreshAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var raw = tokens.ReadRefreshToken(context.Request);
        if (string.IsNullOrEmpty(raw))
        {
            return EndSession(context);
        }
        var old = await sessions.GetRefreshTokenAsync(tokens.Hash(raw), cancellationToken);
        if (old is null || old.ExpiresAtUtc <= now)
        {
            return EndSession(context);
        }
        if (old.RevokedAtUtc is { } revokedAt)
        {
            var live = await ReplayAsync(old, revokedAt, now, cancellationToken);
            if (live is null)
            {
                // Reuse outside the grace window ends this device's session only. Revoking every
                // token the user owned (the old behaviour) meant one stale replay — a phone waking
                // up with a token whose rotation response never arrived — signed the user out on
                // every other device and browser too.
                await sessions.RevokeChainAsync(old, now, cancellationToken);
                return EndSession(context);
            }
            // A benign race, not theft. Rotate off the live tail rather than answering with no
            // token at all: the caller's stored copy is the rotated one, and without a fresh
            // replacement it would expire out of the grace window and strand the device.
            var replayed = await ActiveHouseAsync(live.HouseId, old.UserId, cancellationToken);
            var replayRaw = await RotateAsync(live, old.User, replayed, context, cancellationToken);
            return Response(old.User, replayed,
                await sessions.HasHouseAsync(old.UserId, cancellationToken), replayRaw);
        }
        var active = await ActiveHouseAsync(old.HouseId, old.UserId, cancellationToken);
        var newRaw = await RotateAsync(old, old.User, active, context, cancellationToken);
        var hasHome = await sessions.HasHouseAsync(old.UserId, cancellationToken);
        return Response(old.User, active, hasHome, newRaw);
    }

    public async Task SignOutAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var raw = tokens.ReadRefreshToken(context.Request);
        if (!string.IsNullOrEmpty(raw))
        {
            var token = await sessions.GetRefreshTokenAsync(tokens.Hash(raw), cancellationToken);
            if (token is { RevokedAtUtc: null })
            {
                token.RevokedAtUtc = DateTime.UtcNow;
                await sessions.SaveChangesAsync(cancellationToken);
            }
        }
        tokens.ClearRefreshCookie(context.Response, IsSecure);
    }

    /// <summary>
    /// Decides whether an already-rotated refresh token is a replay of a concurrent refresh rather
    /// than a stolen token. It counts as a replay only inside the configured grace window and only
    /// while the line of succession it started is still alive: a thief who turns up later, or after
    /// the session ended, still trips reuse detection and takes the whole family down.
    /// </summary>
    private async Task<RefreshToken?> ReplayAsync(
        RefreshToken old, DateTime revokedAt, DateTime now, CancellationToken cancellationToken)
    {
        if (revokedAt.AddSeconds(authOptions.Value.RefreshReuseGraceSeconds) < now)
        {
            return null;
        }
        // Follow the chain rather than only the direct replacement: a session that rotates twice
        // inside the grace window leaves a revoked link in the middle, and a straggler holding the
        // first token is still the same live session. The hop limit bounds the query count.
        var next = old.ReplacedByTokenId;
        for (var hop = 0; hop < MaxReplayChainHops && next is { } tokenId; hop++)
        {
            var token = await sessions.GetTokenAsync(tokenId, cancellationToken);
            if (token is null)
            {
                return null;
            }
            if (token.RevokedAtUtc is null)
            {
                return token.ExpiresAtUtc > now ? token : null;
            }
            next = token.ReplacedByTokenId;
        }
        return null;
    }

    // Carry the active home from the rotated token so a session switch survives silent refreshes,
    // but drop it if that home is no longer one the user belongs to.
    private async Task<Guid?> ActiveHouseAsync(Guid? houseId, Guid userId, CancellationToken cancellationToken) =>
        houseId is { } value && await sessions.IsMemberAsync(userId, value, cancellationToken) ? houseId : null;

    // Retires the presented token and issues its successor, linking the two so replay detection
    // can follow the line of succession later.
    private async Task<string> RotateAsync(
        RefreshToken current, User user, Guid? activeHouseId, HttpContext context, CancellationToken cancellationToken)
    {
        var (raw, replacement) = tokens.CreateRefreshToken(user, context, activeHouseId);
        current.RevokedAtUtc = DateTime.UtcNow;
        current.ReplacedByTokenId = replacement.Id;
        sessions.Add(replacement);
        await sessions.SaveChangesAsync(cancellationToken);
        tokens.SetRefreshCookie(context.Response, raw, replacement.ExpiresAtUtc, IsSecure);
        return raw;
    }

    private async Task<string> IssueAsync(
        User user, Guid? activeHouseId, HttpContext context, CancellationToken cancellationToken)
    {
        var (raw, refresh) = tokens.CreateRefreshToken(user, context, activeHouseId);
        sessions.Add(refresh);
        await sessions.SaveChangesAsync(cancellationToken);
        tokens.SetRefreshCookie(context.Response, raw, refresh.ExpiresAtUtc, IsSecure);
        return raw;
    }

    private AuthResponse? EndSession(HttpContext context)
    {
        tokens.ClearRefreshCookie(context.Response, IsSecure);
        return null;
    }

    private AuthResponse Response(User user, Guid? activeHouseId, bool hasHouse, string refreshToken)
    {
        var accessToken = tokens.CreateAccessToken(user, activeHouseId);
        return new(
            CurrentUserResponse.From(user, activeHouseId, hasHouse),
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken);
    }
}
