using Kotlet.Infrastructure.Admin;
using Kotlet.Infrastructure.AgentMemory;
using Kotlet.Infrastructure.Ai;
using Kotlet.Infrastructure.FoodSettings;
using Kotlet.Application.FoodSettings;
using Kotlet.Application.AgentMemory;
using Kotlet.Infrastructure.Auth;
using Kotlet.Application.Images;
using Kotlet.Infrastructure.Houses;
using Kotlet.Infrastructure.Images;
using Kotlet.Infrastructure.Ingredients;
using Kotlet.Infrastructure.MealPlanner;
using Kotlet.Infrastructure.Pantry;
using Kotlet.Infrastructure.PreparedMeals;
using Kotlet.Infrastructure.Persistence;
using Kotlet.Infrastructure.Recipes;
using Kotlet.Infrastructure.RecipeImageSearch;
using Kotlet.Infrastructure.Shopping;
using Kotlet.Infrastructure.Translations;
using Kotlet.Infrastructure.VideoTranscripts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAdminInfrastructure();
        services.AddScoped<IAgentMemoryRepository, AgentMemoryRepository>();
        services.AddAiInfrastructure();
        services.AddScoped<IUserFoodSettingsRepository, UserFoodSettingsRepository>();
        services.AddAuthInfrastructure();
        services.AddHousesInfrastructure();

        services.AddSingleton<IImageProcessor, ImageSharpImageProcessor>();
        services.AddScoped<IStoredImageRepository, StoredImageRepository>();
        services.AddIngredientsInfrastructure();
        services.AddMealPlannerInfrastructure();
        services.AddPantryInfrastructure();
        services.AddPreparedMealsInfrastructure();

        services.AddRecipesInfrastructure();
        services.AddRecipeImageSearchInfrastructure(configuration);
        services.AddShoppingInfrastructure();
        services.AddTranslationsInfrastructure();
        services.AddVideoTranscriptsInfrastructure(configuration);
        services.AddPersistenceInfrastructure(configuration);

        return services;
    }
}
