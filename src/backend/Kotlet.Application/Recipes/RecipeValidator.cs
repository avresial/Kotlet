using Kotlet.Domain.MealPlanner;
using Kotlet.Domain.Recipes;

namespace Kotlet.Application.Recipes;

public static class RecipeValidator
{
    private const int MaxIngredients = 100;
    private const int MaxServings = 99;
    private const int MaxUrlLength = 2000;

    public static Dictionary<string, string[]> Validate(
        string title,
        string? descriptionMarkdown,
        IReadOnlyList<RecipeIngredientRequest> ingredients,
        int servings,
        string? mealType,
        string? sourceUrl) =>
        Validate(title, descriptionMarkdown, ingredients, servings, mealType, sourceUrl, null, null);

    public static Dictionary<string, string[]> Validate(
        string title,
        string? descriptionMarkdown,
        IReadOnlyList<RecipeIngredientRequest> ingredients,
        int servings,
        string? mealType,
        string? sourceUrl,
        string? videoUrl,
        string? videoThumbnailUrl)
    {
        var errors = new Dictionary<string, string[]>();

        ValidateUrl(errors, "sourceUrl", "Source URL", sourceUrl);
        ValidateUrl(errors, "videoUrl", "Video URL", videoUrl);
        ValidateUrl(errors, "videoThumbnailUrl", "Video thumbnail URL", videoThumbnailUrl);
        if (RecipeSourceUrl.Normalize(videoThumbnailUrl) is not null
            && RecipeSourceUrl.Normalize(videoUrl) is null)
        {
            errors["videoThumbnailUrl"] = ["A video thumbnail requires a video URL."];
        }

        if (string.IsNullOrWhiteSpace(title))
            errors["title"] = ["Title is required."];
        else if (title.Trim().Length > 160)
            errors["title"] = ["Title cannot exceed 160 characters."];

        if (servings is < 1 or > MaxServings)
            errors["servings"] = [$"Servings must be between 1 and {MaxServings}."];
        if (!string.IsNullOrWhiteSpace(mealType) && !MealSlotValues.TryParse(mealType, out _))
            errors["mealType"] = ["Meal type is invalid."];

        if (descriptionMarkdown is not null && descriptionMarkdown.Length > 20_000)
            errors["descriptionMarkdown"] = ["Description cannot exceed 20,000 characters."];

        if (ingredients.Count > MaxIngredients)
            errors["ingredients"] = [$"A recipe cannot have more than {MaxIngredients} ingredients."];

        var ingredientErrors = new List<string>();
        for (var i = 0; i < ingredients.Count; i++)
        {
            var ing = ingredients[i];
            if (ing.IngredientId == Guid.Empty)
                ingredientErrors.Add($"Ingredient at position {i + 1}: ingredient is required.");

            if (ing.Quantity <= 0)
                ingredientErrors.Add($"Ingredient at position {i + 1}: quantity must be positive.");

            if (string.IsNullOrWhiteSpace(ing.Unit) || ing.Unit.Trim().Length > 40)
                ingredientErrors.Add($"Ingredient at position {i + 1}: unit is required and cannot exceed 40 characters.");

            if (ing.Note is not null && ing.Note.Trim().Length > 300)
                ingredientErrors.Add($"Ingredient at position {i + 1}: note cannot exceed 300 characters.");
        }
        if (ingredientErrors.Count > 0)
            errors["ingredients"] = ingredientErrors.ToArray();

        return errors;
    }

    private static void ValidateUrl(
        IDictionary<string, string[]> errors,
        string key,
        string displayName,
        string? value)
    {
        var normalizedUrl = RecipeSourceUrl.Normalize(value);
        if (normalizedUrl is null)
        {
            return;
        }

        if (normalizedUrl.Length > MaxUrlLength)
        {
            errors[key] = [$"{displayName} cannot exceed 2,000 characters."];
        }
        else if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors[key] = [$"{displayName} must be an absolute http(s) URL."];
        }
    }
}
