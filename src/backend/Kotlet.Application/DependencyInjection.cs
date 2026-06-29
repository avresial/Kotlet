using Kotlet.Application.Ai;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Measurements;
using Kotlet.Application.Menu.GetMenu;
using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Application.Shopping;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<UserAiProviderService>();
        services.AddScoped<IngredientService>();
        services.AddScoped<MealPlannerService>();
        services.AddSingleton<MeasurementMappingService>();
        services.AddScoped<GetMenuQueryHandler>();
        services.AddScoped<PantryService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<RecipeImageService>();
        services.AddScoped<ShoppingListService>();
        return services;
    }
}
