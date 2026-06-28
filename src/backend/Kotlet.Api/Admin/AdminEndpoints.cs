using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();
        admin.MapGet("/users", GetUsers);
        admin.MapPut("/users/{id:guid}", UpdateUser);
        admin.MapDelete("/users/{id:guid}", DeleteUser);
        return endpoints;
    }

    private static async Task<IResult> GetUsers(KotletDbContext db, CancellationToken cancellationToken)
    {
        var users = await db.Users.AsNoTracking()
            .OrderBy(x => x.DisplayName ?? x.Email)
            .Select(x => new AdminUserResponse(x.Id, x.Email, x.DisplayName, x.PreferredLanguage,
                x.CreatedAtUtc, x.UpdatedAtUtc, x.LastLoginAtUtc))
            .ToListAsync(cancellationToken);
        return Results.Ok(users);
    }

    private static async Task<IResult> UpdateUser(Guid id, UpdateAdminUserRequest request, KotletDbContext db,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.NotFound();

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.NormalizedEmail == normalizedEmail, cancellationToken))
            return Results.Conflict(new { message = "An account with this email already exists." });

        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email.Split('@', 2)[0] : request.DisplayName.Trim();
        user.PreferredLanguage = request.PreferredLanguage?.Trim().ToLowerInvariant();
        user.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> DeleteUser(Guid id, KotletDbContext db, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.NotFound();
        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static Dictionary<string, string[]> Validate(UpdateAdminUserRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Email) || !System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out _))
            errors["email"] = ["A valid email is required."];
        if (request.DisplayName?.Trim().Length > 100)
            errors["displayName"] = ["Display name cannot exceed 100 characters."];
        if (request.PreferredLanguage?.Trim().ToLowerInvariant() is not null and not ("en" or "pl"))
            errors["preferredLanguage"] = ["Preferred language must be 'en' or 'pl'."];
        return errors;
    }

    private static AdminUserResponse ToResponse(User user) => new(user.Id, user.Email, user.DisplayName,
        user.PreferredLanguage, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc);
}

public sealed record UpdateAdminUserRequest(string Email, string? DisplayName, string? PreferredLanguage);
public sealed record AdminUserResponse(Guid Id, string Email, string? DisplayName, string? PreferredLanguage,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? LastLoginAtUtc);
