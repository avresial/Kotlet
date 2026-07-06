using Kotlet.Api.Auth;
using Kotlet.Application.Ai;

namespace Kotlet.Api.Ai;

public sealed record TranslateRequest(string? Text, string? TargetLanguage);

public static class AiTranslationEndpoints
{
    public static IEndpointRouteBuilder MapAiTranslationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/ai/translate", async (
                TranslateRequest request, ICurrentUser user, AiTranslationService service, CancellationToken ct) =>
            {
                if (user.UserId is not { } userId)
                    return Results.Unauthorized();

                var result = await service.TranslateAsync(userId, request.Text, request.TargetLanguage, ct);
                return result.Status switch
                {
                    AiTranslationStatus.Translated => Results.Ok(new { translation = result.Translation }),
                    AiTranslationStatus.InvalidRequest => Results.ValidationProblem(
                        new Dictionary<string, string[]> { ["request"] = [result.Message ?? "Invalid request."] }),
                    AiTranslationStatus.NotConfigured => Results.Problem(
                        title: result.Message, statusCode: StatusCodes.Status409Conflict),
                    _ => Results.Problem(
                        title: result.Message ?? "AI translation failed.", statusCode: StatusCodes.Status502BadGateway)
                };
            })
            .WithTags("AI translation")
            .RequireAuthorization();
        return endpoints;
    }
}
