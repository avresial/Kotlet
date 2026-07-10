using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Recipes;

public static class DiExtension
{
    public static IServiceCollection AddRecipesApplication(this IServiceCollection services) =>
        services.AddScoped<RecipeService>()
            .AddScoped<RecipeDuplicateDetectionService>()
            .AddScoped<RecipeImageService>()
            .AddScoped<RecipeImportService>()
            .AddSingleton<IRecipeImportSignal, RecipeImportSignal>();
}
