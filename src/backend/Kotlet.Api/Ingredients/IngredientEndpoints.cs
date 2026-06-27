using Kotlet.Application.Ingredients;

namespace Kotlet.Api.Ingredients;

public static class IngredientEndpoints
{
    public static IEndpointRouteBuilder MapIngredientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var ingredients = endpoints.MapGroup("/api/ingredients").WithTags("Ingredients").RequireAuthorization();
        ingredients.MapGet("", async (IngredientService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct))).WithName("GetIngredients");
        ingredients.MapGet("/{id:guid}", async (Guid id, IngredientService service, CancellationToken ct) =>
            await service.GetByIdAsync(id, ct) is { } ingredient ? Results.Ok(ingredient) : Results.NotFound())
            .WithName("GetIngredient");
        ingredients.MapPost("", Create).WithName("CreateIngredient");
        ingredients.MapPut("/{id:guid}", Update).WithName("UpdateIngredient");
        ingredients.MapDelete("/{id:guid}", Delete).WithName("DeleteIngredient");
        return endpoints;
    }

    private static async Task<IResult> Create(
        SaveIngredientCommand command,
        IngredientService service,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(command, cancellationToken);
        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> Update(
        Guid id,
        SaveIngredientCommand command,
        IngredientService service,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(id, command, cancellationToken);
        return ToHttpResult(result, created: false);
    }

    private static async Task<IResult> Delete(Guid id, IngredientService service, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) is IngredientOperationStatus.Success
            ? Results.NoContent()
            : Results.NotFound();

    private static IResult ToHttpResult(IngredientOperationResult result, bool created) => result.Status switch
    {
        IngredientOperationStatus.Success when created =>
            Results.Created($"/api/ingredients/{result.Ingredient!.Id}", result.Ingredient),
        IngredientOperationStatus.Success => Results.Ok(result.Ingredient),
        IngredientOperationStatus.NotFound => Results.NotFound(),
        IngredientOperationStatus.Conflict => Results.Conflict(new { result.Message }),
        IngredientOperationStatus.ValidationFailed => Results.ValidationProblem(result.ValidationErrors!),
        _ => throw new InvalidOperationException($"Unsupported ingredient operation status: {result.Status}")
    };
}
