using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public sealed class RecipeImageService(IRecipeImageRepository repository)
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxImages = 10;
    private static readonly HashSet<string> AllowedTypes = ["image/jpeg", "image/png", "image/webp"];
    private static readonly Dictionary<string, string[]> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = [".jpg", ".jpeg"], ["image/png"] = [".png"], ["image/webp"] = [".webp"]
    };

    public async Task<RecipeImageOperationResult> AddAsync(Guid recipeId, Guid ownerUserId, string fileName,
        string contentType, byte[] content, string? altText, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return new(RecipeImageOperationStatus.NotFound);
        var errors = Validate(fileName, contentType, content, altText);
        if (errors.Count > 0) return new(RecipeImageOperationStatus.ValidationFailed, ValidationErrors: errors);
        var count = await repository.CountAsync(recipeId, ct);
        if (count >= MaxImages) return new(RecipeImageOperationStatus.LimitExceeded, ValidationErrors:
            new Dictionary<string, string[]> { ["file"] = [$"A recipe cannot have more than {MaxImages} images."] });
        var image = new RecipeImage
        {
            Id = Guid.NewGuid(), RecipeId = recipeId, FileName = Path.GetFileName(fileName), ContentType = contentType,
            FileSizeBytes = content.LongLength, Content = content, AltText = altText?.Trim(), SortOrder = count,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        repository.Add(image);
        await repository.SaveChangesAsync(ct);
        return new(RecipeImageOperationStatus.Success, ToResponse(image));
    }

    public async Task<IReadOnlyList<RecipeImageResponse>?> ListAsync(Guid recipeId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return null;
        return (await repository.ListAsync(recipeId, false, ct)).Select(ToResponse).ToList();
    }

    public async Task<RecipeImageContent?> GetContentAsync(Guid recipeId, Guid imageId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return null;
        var image = await repository.GetAsync(recipeId, imageId, true, ct);
        return image is null ? null : new(image.FileName, image.ContentType, image.Content);
    }

    public async Task<RecipeImageOperationResult> UpdateAsync(Guid recipeId, Guid imageId, Guid ownerUserId, string? altText, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return new(RecipeImageOperationStatus.NotFound);
        if (altText?.Trim().Length > 300) return Validation("altText", "Alt text cannot exceed 300 characters.");
        var image = await repository.GetAsync(recipeId, imageId, false, ct);
        if (image is null) return new(RecipeImageOperationStatus.NotFound);
        image.AltText = string.IsNullOrWhiteSpace(altText) ? null : altText.Trim();
        image.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await repository.UpdateAltTextAsync(recipeId, imageId, image.AltText, image.UpdatedAtUtc.Value, ct);
        return new(RecipeImageOperationStatus.Success, ToResponse(image));
    }

    public async Task<RecipeImageOperationStatus> ReorderAsync(Guid recipeId, Guid ownerUserId, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return RecipeImageOperationStatus.NotFound;
        var images = await repository.ListAsync(recipeId, false, ct);
        if (ids.Count != images.Count || ids.Distinct().Count() != ids.Count || ids.Any(id => images.All(i => i.Id != id)))
            return RecipeImageOperationStatus.ValidationFailed;
        await repository.UpdateSortOrdersAsync(recipeId, ids, ct);
        return RecipeImageOperationStatus.Success;
    }

    public async Task<RecipeImageOperationStatus> DeleteAsync(Guid recipeId, Guid imageId, Guid ownerUserId, CancellationToken ct)
    {
        if (!await repository.RecipeExistsAsync(recipeId, ownerUserId, ct)) return RecipeImageOperationStatus.NotFound;
        var image = await repository.GetAsync(recipeId, imageId, false, ct);
        if (image is null) return RecipeImageOperationStatus.NotFound;
        await repository.DeleteAsync(recipeId, imageId, ct);
        var remaining = await repository.ListAsync(recipeId, false, ct);
        await repository.UpdateSortOrdersAsync(recipeId, remaining.Select(i => i.Id).ToList(), ct);
        return RecipeImageOperationStatus.Success;
    }

    private static Dictionary<string, string[]> Validate(string fileName, string contentType, byte[] content, string? altText)
    {
        var errors = new Dictionary<string, string[]>();
        if (content.Length == 0) errors["file"] = ["Image file must not be empty."];
        else if (content.LongLength > MaxFileSizeBytes) errors["file"] = [$"Image file cannot exceed {MaxFileSizeBytes / 1024 / 1024} MB."];
        if (!AllowedTypes.Contains(contentType)) errors["contentType"] = ["Only JPEG, PNG, and WebP images are supported."];
        else if (!AllowedExtensions[contentType].Contains(Path.GetExtension(fileName), StringComparer.OrdinalIgnoreCase))
            errors["fileName"] = ["File extension does not match its content type."];
        if (Path.GetFileName(fileName).Length > 260) errors["fileName"] = ["File name cannot exceed 260 characters."];
        if (altText?.Trim().Length > 300) errors["altText"] = ["Alt text cannot exceed 300 characters."];
        return errors;
    }
    private static RecipeImageOperationResult Validation(string key, string message) =>
        new(RecipeImageOperationStatus.ValidationFailed, ValidationErrors: new Dictionary<string, string[]> { [key] = [message] });
    private static RecipeImageResponse ToResponse(RecipeImage i) => new(i.Id, i.RecipeId, i.FileName, i.ContentType,
        i.FileSizeBytes, i.AltText, i.SortOrder, $"/api/recipes/{i.RecipeId}/images/{i.Id}/content", i.CreatedAtUtc);
}
