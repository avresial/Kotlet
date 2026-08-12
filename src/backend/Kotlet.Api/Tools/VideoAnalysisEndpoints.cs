using Kotlet.Api.Auth;
using Kotlet.Application.Tools;

namespace Kotlet.Api.Tools;

public static class VideoAnalysisEndpoints
{
    public static IEndpointRouteBuilder MapVideoAnalysisEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tools/video-transcriptions")
            .WithTags("Tools")
            .RequireAuthorization();

        group.MapPost("", Start).WithName("StartVideoTranscription");
        group.MapGet("/{id:guid}", Get).WithName("GetVideoTranscription");
        group.MapPost("/{id:guid}/continue-as-recipe", ContinueAsRecipe)
            .WithName("ContinueVideoTranscriptionAsRecipe");

        return endpoints;
    }

    private static async Task<IResult> Start(
        StartVideoTranscriptionRequest request,
        ICurrentUser currentUser,
        VideoAnalysisService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId)
        {
            return Results.Unauthorized();
        }

        var result = await service.CreateJobAsync(houseId, userId, request.Url, cancellationToken);
        return result.Status == VideoAnalysisOperationStatus.Success
            ? Results.Accepted($"/api/tools/video-transcriptions/{result.Id}", new { result.Id })
            : Results.ValidationProblem(result.ValidationErrors!);
    }

    private static async Task<IResult> Get(
        Guid id,
        ICurrentUser currentUser,
        VideoAnalysisService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId)
        {
            return Results.Unauthorized();
        }

        var result = await service.GetJobAsync(id, houseId, userId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> ContinueAsRecipe(
        Guid id,
        ICurrentUser currentUser,
        VideoAnalysisService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId)
        {
            return Results.Unauthorized();
        }

        var result = await service.ContinueAsRecipeAsync(id, houseId, userId, cancellationToken);
        return result.Status switch
        {
            VideoAnalysisOperationStatus.Success => Results.Ok(new { result.Id }),
            VideoAnalysisOperationStatus.NotFound => Results.NotFound(),
            VideoAnalysisOperationStatus.InvalidState => Results.Conflict(),
            _ => Results.ValidationProblem(result.ValidationErrors!)
        };
    }
}
