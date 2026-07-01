using Kotlet.Application.Admin;
using Kotlet.Application.Ai;
using Kotlet.Application.FoodSettings;
using Kotlet.Application.Auth;
using Kotlet.Application.Houses;
using Kotlet.Application.Ingredients;
using Kotlet.Application.MealPlanner;
using Kotlet.Application.Measurements;
using Kotlet.Application.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Application.Shopping;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services
            .AddAdminApplication()
            .AddAiApplication()
            .AddScoped<UserFoodSettingsService>()
            .AddAuthApplication()
            .AddHousesApplication()
            .AddIngredientsApplication()
            .AddMealPlannerApplication()
            .AddMeasurementsApplication()
            .AddPantryApplication()
            .AddRecipesApplication()
            .AddShoppingApplication();
    }
}
