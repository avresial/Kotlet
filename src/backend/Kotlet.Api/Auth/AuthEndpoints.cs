using System.Security.Claims;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Auth");
        auth.MapPost("/register", Register);
        auth.MapPost("/login", Login);
        auth.MapGet("/me", Me).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> Register(RegisterRequest request, KotletDbContext dbContext,
        IPasswordHasher<User> passwordHasher, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var errors = ValidateRegistration(request);
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var email = request.Email.Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (await dbContext.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
            return Results.Conflict(new { message = "An account with this email already exists." });

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(), Email = email, NormalizedEmail = normalizedEmail, PasswordHash = string.Empty,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            CreatedAtUtc = now, UpdatedAtUtc = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Results.Conflict(new { message = "An account with this email already exists." });
        }

        await SignIn(httpContext, user);
        return Results.Created("/api/auth/me", new AuthResponse(ToResponse(user)));
    }

    private static async Task<IResult> Login(LoginRequest request, KotletDbContext dbContext,
        IPasswordHasher<User> passwordHasher, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["credentials"] = ["Email and password are required."] });

        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == NormalizeEmail(request.Email), cancellationToken);
        var valid = user is not null && passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            is not PasswordVerificationResult.Failed;
        if (!valid)
            return Results.Unauthorized();

        user!.LastLoginAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = user.LastLoginAtUtc.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        await SignIn(httpContext, user);
        return Results.Ok(new AuthResponse(ToResponse(user)));
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, KotletDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Unauthorized();

        var user = await dbContext.Users.AsNoTracking().SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
    }

    private static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
            errors["email"] = ["A valid email is required."];
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            errors["password"] = ["Password must be at least 8 characters long."];
        if (request.Password != request.ConfirmPassword)
            errors["confirmPassword"] = ["Passwords do not match."];
        if (request.DisplayName?.Trim().Length > 100)
            errors["displayName"] = ["Display name cannot exceed 100 characters."];
        return errors;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static Task SignIn(HttpContext context, User user) => context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Email, user.Email)],
            CookieAuthenticationDefaults.AuthenticationScheme)));

    private static CurrentUserResponse ToResponse(User user) =>
        new(user.Id, user.Email, user.DisplayName, user.CreatedAtUtc, user.LastLoginAtUtc);
}
