namespace Kotlet.Domain.Recipes;

public sealed class Recipe
{
    public Guid Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Title { get; set; }
    public required string Slug { get; set; }
    public string? DescriptionMarkdown { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public ICollection<RecipeIngredient> Ingredients { get; set; } = [];
}
