using Kotlet.Api.Auth;
using Kotlet.Application.Admin;
using Kotlet.Application.Settings;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.VideoTranscripts;

namespace Kotlet.Api.Admin;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization(RoleNames.Admin);
        admin.MapGet("/users", GetUsers);
        admin.MapPut("/users/{id:guid}", UpdateUser);
        admin.MapDelete("/users/{id:guid}", DeleteUser);
        admin.MapGet("/settings/youtube-transcription", GetYoutubeTranscriptionSettings);
        admin.MapPut("/settings/youtube-transcription", SaveYoutubeTranscriptionSettings);
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

    private static async Task<IResult> GetYoutubeTranscriptionSettings(
        ISystemSettingsStore settings, SupadataOptions options, CancellationToken cancellationToken)
    {
        var apiKey = await settings.GetAsync(SystemSettingKeys.SupadataApiKey, cancellationToken) ?? options.ApiKey;
        return Results.Ok(new { hasApiKey = !string.IsNullOrWhiteSpace(apiKey) });
    }

    private static async Task<IResult> SaveYoutubeTranscriptionSettings(
        SaveYoutubeTranscriptionSettingsRequest request,
        ISystemSettingsStore settings,
        SupadataOptions options,
        CancellationToken cancellationToken)
    {
        var apiKey = request.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var existing = await settings.GetAsync(SystemSettingKeys.SupadataApiKey, cancellationToken) ?? options.ApiKey;
            if (string.IsNullOrWhiteSpace(existing))
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["apiKey"] = ["API key is required."] });
        }
        else
        {
            if (apiKey.Length > 4096)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["apiKey"] = ["API key cannot exceed 4096 characters."] });
            await settings.SetAsync(SystemSettingKeys.SupadataApiKey, apiKey, cancellationToken);
        }

        return Results.Ok(new { hasApiKey = true });
    }

    private sealed record SaveYoutubeTranscriptionSettingsRequest(string? ApiKey);
}
