using Kotlet.Application.RecipeImageSearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.RecipeImageSearch;

public static class DiExtension
{
    public static IServiceCollection AddRecipeImageSearchInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = new PexelsOptions();
        configuration.GetSection(PexelsOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        var recipeImages = new RecipeImagesOptions();
        configuration.GetSection(RecipeImagesOptions.SectionName).Bind(recipeImages);
        services.AddSingleton(recipeImages);

        services.AddHttpClient<PexelsRecipeImageProvider>(client =>
        {
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl)
                ? PexelsOptions.DefaultBaseUrl
                : options.BaseUrl;
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        });
        services.AddScoped<IRecipeImageProvider>(sp =>
            sp.GetRequiredService<PexelsRecipeImageProvider>());
        return services;
    }
}
