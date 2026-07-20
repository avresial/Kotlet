namespace Kotlet.Application.Recipes;

/// <summary>Importance tiers for missing recipe data, ordered from most to least severe.</summary>
public static class RecipeAuditImportance
{
    public const string Important = "important";
    public const string Minor = "minor";
}

/// <summary>Names of recipe elements the audit can report as missing.</summary>
public static class RecipeAuditElements
{
    public const string Ingredients = "ingredients";
    public const string Description = "description";
    public const string Image = "image";
    public const string MealType = "mealType";
}

public sealed record RecipeAuditItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string Importance,
    IReadOnlyList<string> MissingElements,
    int MissingCount);
