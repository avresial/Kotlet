using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Recipes;
using Kotlet.Domain.MealPlanner;

namespace Kotlet.Api.Recipes;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var recipes = endpoints.MapGroup("/api/recipes").WithTags("Recipes").RequireAuthorization();
        recipes.MapGet("", List).WithName("ListRecipes");
        recipes.MapGet("/recent", ListRecent).WithName("ListRecentRecipes");
        recipes.MapPost("", Create).WithName("CreateRecipe");
        recipes.MapGet("/{id:guid}", GetById).WithName("GetRecipe");
        recipes.MapPut("/{id:guid}", Update).WithName("UpdateRecipe");
        recipes.MapDelete("/{id:guid}", Delete).WithName("DeleteRecipe");
        recipes.MapPost("/{recipeId:guid}/images", UploadImage).DisableAntiforgery().WithName("UploadRecipeImage");
        recipes.MapGet("/{recipeId:guid}/images", ListImages).WithName("ListRecipeImages");
        recipes.MapGet("/{recipeId:guid}/images/{imageId:guid}/content", GetImageContent).WithName("GetRecipeImageContent");
        recipes.MapPut("/{recipeId:guid}/images/order", ReorderImages).WithName("ReorderRecipeImages");
        recipes.MapPatch("/{recipeId:guid}/images/{imageId:guid}", UpdateImage).WithName("UpdateRecipeImage");
        recipes.MapDelete("/{recipeId:guid}/images/{imageId:guid}", DeleteImage).WithName("DeleteRecipeImage");
        return endpoints;
    }

    private static async Task<IResult> UploadImage(Guid recipeId, IFormFile file, string? altText,
        ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        if (file.Length > RecipeImageService.MaxFileSizeBytes)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Image file cannot exceed 5 MB."] });
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        return ToImageHttpResult(await service.AddAsync(recipeId, houseId, file.FileName, file.ContentType,
            memory.ToArray(), altText, ct), true);
    }

    private static async Task<IResult> ListImages(Guid recipeId, ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var images = await service.ListAsync(recipeId, houseId, ct);
        return images is null ? Results.NotFound() : Results.Ok(images);
    }

    private static async Task<IResult> GetImageContent(Guid recipeId, Guid imageId, ICurrentUser currentUser,
        RecipeImageService service, HttpContext context, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var image = await service.GetContentAsync(recipeId, imageId, houseId, ct);
        if (image is null) return Results.NotFound();
        context.Response.Headers.CacheControl = "private,max-age=86400";
        return Results.File(image.Content, image.ContentType, image.FileName, enableRangeProcessing: true);
    }

    private static async Task<IResult> ReorderImages(Guid recipeId, ReorderRecipeImagesRequest request,
        ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return await service.ReorderAsync(recipeId, houseId, request.ImageIds, ct) switch
        {
            RecipeImageOperationStatus.Success => Results.NoContent(),
            RecipeImageOperationStatus.NotFound => Results.NotFound(),
            _ => Results.ValidationProblem(new Dictionary<string, string[]> { ["imageIds"] = ["Image ids must contain every recipe image exactly once."] })
        };
    }

    private static async Task<IResult> UpdateImage(Guid recipeId, Guid imageId, UpdateRecipeImageRequest request,
        ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return ToImageHttpResult(await service.UpdateAsync(recipeId, imageId, houseId, request.AltText, ct), false);
    }

    private static async Task<IResult> DeleteImage(Guid recipeId, Guid imageId, ICurrentUser currentUser,
        RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return await service.DeleteAsync(recipeId, imageId, houseId, ct) is RecipeImageOperationStatus.Success
            ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> List(
        ICurrentUser currentUser,
        RecipeService service,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        string? mealType = null,
        Guid[]? ingredientIds = null,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        if (!string.IsNullOrWhiteSpace(mealType) && !MealSlotValues.TryParse(mealType, out _))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["mealType"] = ["Meal type is invalid."] });
        ingredientIds = ingredientIds?.Distinct().ToArray();
        if (ingredientIds?.Length > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["ingredientIds"] = ["No more than 100 ingredients can be selected."] });
        var result = await service.ListAsync(houseId, page, pageSize, search, mealType, ingredientIds, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListRecent(
        ICurrentUser currentUser,
        RecipeService service,
        int limit = 4,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return Results.Ok(await service.ListRecentAsync(houseId, limit, cancellationToken));
    }

    private static async Task<IResult> Create(
        CreateRecipeRequest request,
        ICurrentUser currentUser,
        RecipeService service,
        ILanguageContext language,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId || currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.CreateAsync(userId, houseId, request, cancellationToken, language.Language);
        return ToHttpResult(result, created: true);
    }

    private static async Task<IResult> GetById(
        Guid id,
        ICurrentUser currentUser,
        RecipeService service,
        ILanguageContext language,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var recipe = await service.GetByIdAsync(id, houseId, cancellationToken, language.Language);
        return recipe is null ? Results.NotFound() : Results.Ok(recipe);
    }

    private static async Task<IResult> Update(
        Guid id,
        UpdateRecipeRequest request,
        ICurrentUser currentUser,
        RecipeService service,
        ILanguageContext language,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var result = await service.UpdateAsync(id, houseId, request, cancellationToken, language.Language);
        return ToHttpResult(result, created: false);
    }

    private static async Task<IResult> Delete(
        Guid id,
        ICurrentUser currentUser,
        RecipeService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return await service.DeleteAsync(id, houseId, cancellationToken) is RecipeOperationStatus.Success
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

    private static IResult ToImageHttpResult(RecipeImageOperationResult result, bool created) => result.Status switch
    {
        RecipeImageOperationStatus.Success when created => Results.Created(result.Image!.ContentUrl, result.Image),
        RecipeImageOperationStatus.Success => Results.Ok(result.Image),
        RecipeImageOperationStatus.NotFound => Results.NotFound(),
        RecipeImageOperationStatus.ValidationFailed or RecipeImageOperationStatus.LimitExceeded =>
            Results.ValidationProblem(result.ValidationErrors!),
        _ => throw new InvalidOperationException()
    };
}
