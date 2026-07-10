namespace Kotlet.Infrastructure.RecipeImageSearch;

public sealed class PexelsOptions
{
    public const string SectionName = "Pexels";
    public const string DefaultBaseUrl = "https://api.pexels.com/";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
