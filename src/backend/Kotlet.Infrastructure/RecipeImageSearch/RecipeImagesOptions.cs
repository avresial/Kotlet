namespace Kotlet.Infrastructure.RecipeImageSearch;

public sealed class RecipeImagesOptions
{
    public const string SectionName = "RecipeImages";

    public string Provider { get; set; } = PexelsRecipeImageProvider.ProviderName;
}
