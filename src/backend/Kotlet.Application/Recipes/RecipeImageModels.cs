namespace Kotlet.Application.Recipes;

public sealed record RecipeImageResponse(
    Guid Id, Guid RecipeId, string FileName, string ContentType, long FileSizeBytes,
    string? AltText, int SortOrder, string ContentUrl, DateTimeOffset CreatedAtUtc);

public sealed record RecipeImageContent(string FileName, string ContentType, byte[] Content);
public sealed record UpdateRecipeImageRequest(string? AltText);
public sealed record ReorderRecipeImagesRequest(IReadOnlyList<Guid> ImageIds);

public enum RecipeImageOperationStatus { Success, NotFound, ValidationFailed, LimitExceeded }

public sealed record RecipeImageOperationResult(
    RecipeImageOperationStatus Status,
    RecipeImageResponse? Image = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
