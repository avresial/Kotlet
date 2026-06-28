using Kotlet.Api.Auth;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(RoleNames.Admin);
        admin.MapGet("/users", GetUsers);
        admin.MapPut("/users/{id:guid}", UpdateUser);
        admin.MapDelete("/users/{id:guid}", DeleteUser);
        return endpoints;
    }

    private static async Task<IResult> GetUsers(KotletDbContext db, CancellationToken cancellationToken,
        int page = 1, string? search = null)
    {
        page = Math.Max(1, page);
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Email.ToLower().Contains(term) ||
                (x.DisplayName != null && x.DisplayName.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(x => x.DisplayName ?? x.Email)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * 10)
            .Take(10)
            .Select(x => new AdminUserResponse(x.Id, x.Email, x.DisplayName, x.PreferredLanguage,
                x.CreatedAtUtc, x.UpdatedAtUtc, x.LastLoginAtUtc,
                x.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToArray()))
            .ToListAsync(cancellationToken);
        return Results.Ok(new AdminUserPageResponse(users, page, 10, totalCount));
    }

    private static async Task<IResult> UpdateUser(Guid id, UpdateAdminUserRequest request, ICurrentUser currentUser,
        KotletDbContext db, CancellationToken cancellationToken)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null) return Results.NotFound();

        var availableRoles = await db.Roles.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var requestedRoles = request.Roles!.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var unknownRoles = requestedRoles.Where(role => !availableRoles.ContainsKey(role)).ToArray();
        if (unknownRoles.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["One or more roles are invalid."] });
        if (currentUser.UserId == id && user.Roles.Any(role => role.Name == RoleNames.Admin) &&
            !requestedRoles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["roles"] = ["You cannot remove your own Admin role."] });

        var email = request.Email.Trim();
        var normalizedEmail = email.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.Id != id && x.NormalizedEmail == normalizedEmail, cancellationToken))
            return Results.Conflict(new { message = "An account with this email already exists." });

        user.Email = email;
        user.NormalizedEmail = normalizedEmail;
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email.Split('@', 2)[0] : request.DisplayName.Trim();
        user.PreferredLanguage = request.PreferredLanguage?.Trim().ToLowerInvariant();
        foreach (var role in user.Roles.Where(role => !requestedRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase)).ToArray())
            user.Roles.Remove(role);
        foreach (var roleName in requestedRoles.Where(role => user.Roles.All(current => !current.Name.Equals(role, StringComparison.OrdinalIgnoreCase))))
            user.Roles.Add(availableRoles[roleName]);
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
        if (request.Roles is null)
            errors["roles"] = ["Roles are required."];
        return errors;
    }

    private static AdminUserResponse ToResponse(User user) => new(user.Id, user.Email, user.DisplayName,
        user.PreferredLanguage, user.CreatedAtUtc, user.UpdatedAtUtc, user.LastLoginAtUtc,
        user.Roles.OrderBy(role => role.Name).Select(role => role.Name).ToArray());
}

public sealed record UpdateAdminUserRequest(string Email, string? DisplayName, string? PreferredLanguage, string[]? Roles);
public sealed record AdminUserResponse(Guid Id, string Email, string? DisplayName, string? PreferredLanguage,
    DateTime CreatedAtUtc, DateTime UpdatedAtUtc, DateTime? LastLoginAtUtc, string[] Roles);
public sealed record AdminUserPageResponse(IReadOnlyList<AdminUserResponse> Items, int Page, int PageSize, int TotalCount);
