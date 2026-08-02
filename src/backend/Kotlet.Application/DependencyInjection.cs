using Kotlet.Application.Admin;
using Kotlet.Application.AgentMemory;
using Kotlet.Application.Ai;
using Kotlet.Application.FoodSettings;
using Kotlet.Application.Auth;
using Kotlet.Application.Houses;
using Kotlet.Application.Ingredients;
using Kotlet.Application.Images;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Measurements;
using Kotlet.Application.Pantry;
using Kotlet.Application.PreparedMeals;
using Kotlet.Application.Recipes;
using Kotlet.Application.RecipeImageSearch;
using Kotlet.Application.Shopping;
using Kotlet.Application.VideoTranscripts;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddAdminApplication();
        services.AddScoped<AgentMemoryService>();
        services.AddAiApplication();
        services.AddScoped<UserFoodSettingsService>();
        services.AddAuthApplication();
        services.AddHousesApplication();
        services.AddIngredientsApplication();
        services.AddScoped<StoredImageService>();
        services.AddMealPlannerApplication();
        services.AddMeasurementsApplication();
        services.AddPantryApplication();
        services.AddPreparedMealsApplication();
        services.AddRecipesApplication();
        services.AddRecipeImageSearchApplication();
        services.AddShoppingApplication();
        services.AddVideoTranscriptsApplication();

        return services;
    }
}
