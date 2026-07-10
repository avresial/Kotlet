using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.RecipeImageSearch;

public static class DiExtension
{
    public static IServiceCollection AddRecipeImageSearchApplication(this IServiceCollection services) =>
        services.AddScoped<RecipeImageSearchService>();
}
