using Kotlet.Api.Auth;
using Kotlet.Application.Admin;
using Kotlet.Domain.Auth;

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

    private static async Task<IResult> GetUsers(
        AdminUserService service, CancellationToken cancellationToken, int page = 1, string? search = null) =>
        Results.Ok(await service.GetUsersAsync(page, search, cancellationToken));

    private static async Task<IResult> UpdateUser(
        Guid id,
        UpdateAdminUserRequest request,
        ICurrentUser currentUser,
        AdminUserService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } currentUserId) return Results.Unauthorized();
        var result = await service.UpdateAsync(id, currentUserId, request, cancellationToken);
        return result.Status switch
        {
            AdminUserOperationStatus.Success => Results.Ok(result.User),
            AdminUserOperationStatus.NotFound => Results.NotFound(),
            AdminUserOperationStatus.Conflict => Results.Conflict(new { message = "An account with this email already exists." }),
            AdminUserOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
            _ => throw new InvalidOperationException($"Unsupported admin user operation status: {result.Status}")
        };
    }

    private static async Task<IResult> DeleteUser(
        Guid id, AdminUserService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) is AdminUserOperationStatus.Success
            ? Results.NoContent()
            : Results.NotFound();
}
