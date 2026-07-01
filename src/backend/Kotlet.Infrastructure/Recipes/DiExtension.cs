using Kotlet.Application.Recipes;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Recipes;

public static class DiExtension
{
    public static IServiceCollection AddRecipesInfrastructure(this IServiceCollection services) => services
        .AddScoped<IRecipeRepository, RecipeRepository>()
        .AddScoped<IRecipeImageRepository, RecipeImageRepository>();
}
