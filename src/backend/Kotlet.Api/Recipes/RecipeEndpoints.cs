using Kotlet.Api.Auth;
using Kotlet.Application.Recipes;

namespace Kotlet.Api.Recipes;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var recipes = endpoints.MapGroup("/api/recipes").WithTags("Recipes").RequireAuthorization();
        recipes.MapGet("", List).WithName("ListRecipes");
        recipes.MapPost("", Create).WithName("CreateRecipe");
        recipes.MapGet("/{id:guid}", GetById).WithName("GetRecipe");
        recipes.MapPut("/{id:guid}", Update).WithName("UpdateRecipe");
        recipes.MapDelete("/{id:guid}", Delete).WithName("DeleteRecipe");
        return endpoints;
    }

    private static async Task<IResult> List(
        ICurrentUser currentUser,
        RecipeService service,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.ListAsync(userId, page, pageSize, search, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateRecipeRequest request,
        ICurrentUser currentUser,
        RecipeService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.CreateAsync(userId, request, cancellationToken);
        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ICurrentUser currentUser,
        RecipeService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var recipe = await service.GetByIdAsync(id, userId, cancellationToken);
        return recipe is null ? Results.NotFound() : Results.Ok(recipe);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateRecipeRequest request,
        ICurrentUser currentUser,
        RecipeService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        var result = await service.UpdateAsync(id, userId, request, cancellationToken);
        return ToHttpResult(result, created: false);
    }

    private static async Task<IResult> Delete(
        Guid id,
        ICurrentUser currentUser,
        RecipeService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId) return Results.Unauthorized();
        return await service.DeleteAsync(id, userId, cancellationToken) is RecipeOperationStatus.Success
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static IResult ToHttpResult(RecipeOperationResult result, bool created) => result.Status switch
    {
        RecipeOperationStatus.Success when created =>
            Results.Created($"/api/recipes/{result.Recipe!.Id}", result.Recipe),
        RecipeOperationStatus.Success => Results.Ok(result.Recipe),
        RecipeOperationStatus.NotFound => Results.NotFound(),
        RecipeOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
        _ => throw new InvalidOperationException($"Unsupported recipe operation status: {result.Status}")
    };
}
