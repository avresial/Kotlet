using Kotlet.Api.Auth;
using Kotlet.Api.Localization;
using Kotlet.Application.Recipes;
using Kotlet.Application.RecipeImageSearch;
using Kotlet.Domain.MealPlanner;
using Microsoft.AspNetCore.Mvc;

namespace Kotlet.Api.Recipes;

public static class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var recipes = endpoints.MapGroup("/api/recipes").WithTags("Recipes").RequireAuthorization();
        recipes.MapRecipeImportEndpoints();
        recipes.MapGet("/images/search", SearchImages).WithName("SearchRecipeImages");
        recipes.MapPost("/images/import", ImportImage).WithName("ImportRecipeImage");
        recipes.MapGet("", List).WithName("ListRecipes");
        recipes.MapGet("/recent", ListRecent).WithName("ListRecentRecipes");
        recipes.MapGet("/audit", ListAudit).WithName("ListRecipeAudit");
        recipes.MapPost("", Create).WithName("CreateRecipe");
        recipes.MapGet("/{id:guid}", GetById).AllowAnonymous().WithName("GetRecipe");
        recipes.MapPut("/{id:guid}", Update).WithName("UpdateRecipe");
        recipes.MapDelete("/{id:guid}", Delete).WithName("DeleteRecipe");
        recipes.MapPost("/{recipeId:guid}/images", UploadImage).DisableAntiforgery().WithName("UploadRecipeImage");
        recipes.MapGet("/{recipeId:guid}/images", ListImages).WithName("ListRecipeImages");
        recipes.MapGet("/{recipeId:guid}/images/{imageId:guid}/content", GetImageContent).AllowAnonymous().WithName("GetRecipeImageContent");
        recipes.MapPut("/{recipeId:guid}/images/order", ReorderImages).WithName("ReorderRecipeImages");
        recipes.MapPatch("/{recipeId:guid}/images/{imageId:guid}", UpdateImage).WithName("UpdateRecipeImage");
        recipes.MapDelete("/{recipeId:guid}/images/{imageId:guid}", DeleteImage).WithName("DeleteRecipeImage");
        return endpoints;
    }

    private static async Task<IResult> UploadImage(Guid recipeId, IFormFile file, [FromForm] string? altText,
        [FromForm] string? sourceProvider, [FromForm] string? sourceExternalId, [FromForm] string? sourceUrl,
        [FromForm] string? sourceAuthorName, [FromForm] string? sourceAuthorUrl,
        ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        if (file.Length > RecipeImageService.MaxFileSizeBytes)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["file"] = ["Image file cannot exceed 5 MB."] });
        await using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, ct);
        RecipeImageSourceData? source = null;
        if (new[] { sourceProvider, sourceExternalId, sourceUrl, sourceAuthorName, sourceAuthorUrl }
            .Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            source = new RecipeImageSourceData(sourceProvider ?? string.Empty, sourceExternalId, sourceUrl,
                sourceAuthorName, sourceAuthorUrl);
        }
        return ToImageHttpResult(await service.AddAsync(recipeId, houseId, file.FileName, file.ContentType,
            memory.ToArray(), altText, ct, source), true);
    }

    private static async Task<IResult> SearchImages(
        ICurrentUser currentUser,
        RecipeImageSearchService service,
        CancellationToken cancellationToken,
        string? query = null,
        int limit = RecipeImageSearchService.DefaultLimit,
        string? orientation = "landscape",
        string? locale = null)
    {
        if (currentUser.HouseId is null) return Results.Unauthorized();

        var result = await service.SearchAsync(
            new RecipeImageSearchRequest(query ?? string.Empty, limit, orientation, locale), cancellationToken);
        return result.Status switch
        {
            RecipeImageSearchStatus.Success => Results.Ok(result.Candidates),
            RecipeImageSearchStatus.InvalidQuery => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["query"] = [result.Message ?? "A query is required."] }),
            RecipeImageSearchStatus.NotConfigured => Results.Problem(
                result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            RecipeImageSearchStatus.RateLimited => Results.Problem(
                result.Message, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.Problem(result.Message, statusCode: StatusCodes.Status502BadGateway)
        };
    }

    private static async Task<IResult> ImportImage(
        RecipeImageImportRequest request,
        ICurrentUser currentUser,
        RecipeImageImportService service,
        CancellationToken cancellationToken)
    {
        if (currentUser.HouseId is null) return Results.Unauthorized();

        var result = await service.ImportAsync(request, cancellationToken);
        return result.Status switch
        {
            RecipeImageImportStatus.Success => Results.Ok(result.Image),
            RecipeImageImportStatus.InvalidRequest => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["request"] = [result.Message ?? "The image import request is invalid."] }),
            RecipeImageImportStatus.NotConfigured => Results.Problem(
                result.Message, statusCode: StatusCodes.Status503ServiceUnavailable),
            RecipeImageImportStatus.NotFound => Results.NotFound(),
            RecipeImageImportStatus.RateLimited => Results.Problem(
                result.Message, statusCode: StatusCodes.Status429TooManyRequests),
            _ => Results.Problem(result.Message, statusCode: StatusCodes.Status502BadGateway)
        };
    }

    private static async Task<IResult> ListImages(Guid recipeId, ICurrentUser currentUser, RecipeImageService service, CancellationToken ct)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        var images = await service.ListAsync(recipeId, houseId, ct);
        return images is null ? Results.NotFound() : Results.Ok(images);
    }

    private static async Task<IResult> GetImageContent(Guid recipeId, Guid imageId,
        RecipeImageService service, HttpContext context, CancellationToken ct)
    {
        var image = await service.GetPublicContentAsync(recipeId, imageId, ct);
        if (image is null) return Results.NotFound();
        context.Response.Headers.CacheControl = "public,max-age=86400";
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

    private static async Task<IResult> ListAudit(
        ICurrentUser currentUser,
        RecipeAuditService service,
        int limit = RecipeAuditService.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        if (currentUser.HouseId is not { } houseId) return Results.Unauthorized();
        return Results.Ok(await service.ListRecipesRequiringFixAsync(houseId, limit, cancellationToken));
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
        var recipe = await service.GetPublicByIdAsync(id, currentUser.HouseId, cancellationToken, language.Language);
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
