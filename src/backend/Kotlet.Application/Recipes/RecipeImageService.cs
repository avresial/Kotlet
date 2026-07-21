using Kotlet.Application.Images;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeImageService(IRecipeImageRepository repository, StoredImageService imageStorage)
{
    public const long MaxFileSizeBytes = StoredImageService.MaxFileSizeBytes;
    public const int MaxImages = StoredImageService.MaxImagesPerOwner;
    public const int ProcessedMaxWidth = StoredImageService.ProcessedMaxWidth;
    public const int ProcessedMaxHeight = StoredImageService.ProcessedMaxHeight;

    public async Task<RecipeImageOperationResult> AddAsync(Guid recipeId, Guid ownerUserId, string fileName,
        string contentType, byte[] content, string? altText, CancellationToken ct, RecipeImageSourceData? source = null)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return new(RecipeImageOperationStatus.NotFound);
        var count = await repository.CountAsync(recipeId, ct);
        if (count >= MaxImages) return new(RecipeImageOperationStatus.LimitExceeded, ValidationErrors:
            new Dictionary<string, string[]> { ["file"] = [$"A recipe cannot have more than {MaxImages} images."] });
        var stored = await imageStorage.CreateAsync(fileName, contentType, content, altText,
            source is null ? null : new(source.Provider, source.ExternalId, source.Url, source.AuthorName, source.AuthorUrl), ct);
        if (stored.Errors is not null) return new(RecipeImageOperationStatus.ValidationFailed, ValidationErrors: stored.Errors);
        var image = new RecipeImage
        {
            Id = stored.Image!.Id,
            RecipeId = recipeId,
            Image = stored.Image,
            SortOrder = count,
        };
        repository.Add(image);
        await repository.SaveChangesAsync(ct);
        return new(RecipeImageOperationStatus.Success, ToResponse(image));
    }

    public async Task<IReadOnlyList<RecipeImageResponse>?> ListAsync(Guid recipeId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return null;
        return (await repository.ListAsync(recipeId, ct)).Select(ToResponse).ToList();
    }

    public async Task<RecipeImageContent?> GetContentAsync(Guid recipeId, Guid imageId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return null;
        if (await repository.GetAsync(recipeId, imageId, ct) is null) return null;
        return await imageStorage.GetContentAsync(imageId, ct) is { } content ? new(content.FileName, content.ContentType, content.Content) : null;
    }

    public async Task<RecipeImageContent?> GetPublicContentAsync(Guid recipeId, Guid imageId, CancellationToken ct)
    {
        if (await repository.GetAsync(recipeId, imageId, ct) is null) return null;
        return await imageStorage.GetContentAsync(imageId, ct) is { } content ? new(content.FileName, content.ContentType, content.Content) : null;
    }

    public async Task<RecipeImageOperationResult> UpdateAsync(Guid recipeId, Guid imageId, Guid ownerUserId, string? altText, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return new(RecipeImageOperationStatus.NotFound);
        var image = await repository.GetAsync(recipeId, imageId, ct);
        if (image is null) return new(RecipeImageOperationStatus.NotFound);
        if (!await imageStorage.UpdateAltTextAsync(imageId, altText, ct)) return Validation("altText", "Alt text cannot exceed 300 characters.");
        image.Image.AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        return new(RecipeImageOperationStatus.Success, ToResponse(image));
    }

    public async Task<RecipeImageOperationStatus> ReorderAsync(Guid recipeId, Guid ownerUserId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return RecipeImageOperationStatus.NotFound;
        var images = await repository.ListAsync(recipeId, ct);
        if (ids.Count != images.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => images.All(i => i.Id != id)))
            return RecipeImageOperationStatus.ValidationFailed;
        await repository.UpdateSortOrdersAsync(recipeId, ids, ct);
        return RecipeImageOperationStatus.Success;
    }

    public async Task<RecipeImageOperationStatus> DeleteAsync(Guid recipeId, Guid imageId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return RecipeImageOperationStatus.NotFound;
        var image = await repository.GetAsync(recipeId, imageId, ct);
        if (image is null) return RecipeImageOperationStatus.NotFound;
        await imageStorage.DeleteAsync(imageId, ct);
        var remaining = await repository.ListAsync(recipeId, ct);
        await repository.UpdateSortOrdersAsync(recipeId, remaining.Select(i => i.Id).ToList(), ct);
        return RecipeImageOperationStatus.Success;
    }

    private static RecipeImageOperationResult Validation(string key, string message) =>
        new(RecipeImageOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });
    private static RecipeImageResponse ToResponse(RecipeImage i) => new(i.Id, i.RecipeId, i.Image.FileName, i.Image.ContentType,
        i.Image.FileSizeBytes, i.Image.AltText, i.SortOrder, $"/api/recipes/{i.RecipeId}/images/{i.Id}/content", i.Image.CreatedAtUtc,
        SourceAttributionResponse.FromPrimarySource(i));
}
