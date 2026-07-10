using Kotlet.Api.Auth;
using Kotlet.Application.Recipes;

namespace Kotlet.Api.Recipes;

public static class RecipeImportEndpoints
{
    public static RouteGroupBuilder MapRecipeImportEndpoints(this RouteGroupBuilder recipes)
    {
        recipes.MapPost("/import", Start).WithName("StartRecipeImport");
        recipes.MapGet("/import/{id:guid}", Get).WithName("GetRecipeImport");
        recipes.MapPost("/import/{id:guid}/accept", Accept).WithName("AcceptRecipeImport");
        return recipes;
    }

    private static async Task<IResult> Start(StartRecipeImportRequest request, ICurrentUser currentUser,
        RecipeImportService service, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.CreateJobAsync(houseId, userId, request.Url, cancellationToken);
        return result.Status == RecipeImportOperationStatus.Success
            ? Results.Accepted($"/api/recipes/import/{result.Id}", new { result.Id })
            : Results.ValidationProblem(result.ValidationErrors!);
    }

    private static async Task<IResult> Get(Guid id, ICurrentUser currentUser,
        RecipeImportService service, CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.GetJobAsync(id, houseId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Accept(Guid id, RecipeImportDraft draft, ICurrentUser currentUser,
        RecipeImportService service, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.AcceptAsync(id, houseId, userId, draft, cancellationToken);
        return result.Status switch
        {
            RecipeImportOperationStatus.Success => Results.Created($"/api/recipes/{result.Id}", new { result.Id }),
            RecipeImportOperationStatus.NotFound => Results.NotFound(),
            RecipeImportOperationStatus.InvalidState => Results.Conflict(),
            _ => Results.ValidationProblem(result.ValidationErrors!)
        };
    }
}
