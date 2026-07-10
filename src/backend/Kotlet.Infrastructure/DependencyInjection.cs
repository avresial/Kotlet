using Kotlet.Infrastructure.Admin;
using Kotlet.Infrastructure.Ai;
using Kotlet.Infrastructure.FoodSettings;
using Kotlet.Application.FoodSettings;
using Kotlet.Infrastructure.Auth;
using Kotlet.Application.Images;
using Kotlet.Infrastructure.Houses;
using Kotlet.Infrastructure.Images;
using Kotlet.Infrastructure.Ingredients;
using Kotlet.Infrastructure.MealPlanner;
using Kotlet.Infrastructure.Pantry;
using Kotlet.Infrastructure.Persistence;
using Kotlet.Infrastructure.Recipes;
using Kotlet.Infrastructure.Shopping;
using Kotlet.Infrastructure.Translations;
using Kotlet.Infrastructure.VideoTranscripts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration) => services
        .AddAdminInfrastructure()
        .AddAiInfrastructure()
        .AddScoped<IUserFoodSettingsRepository, UserFoodSettingsRepository>()
        .AddAuthInfrastructure()
        .AddHousesInfrastructure()
        .AddSingleton<IImageProcessor, ImageSharpImageProcessor>()
        .AddIngredientsInfrastructure()
        .AddMealPlannerInfrastructure()
        .AddPantryInfrastructure()
        .AddRecipesInfrastructure()
        .AddShoppingInfrastructure()
        .AddTranslationsInfrastructure()
        .AddVideoTranscriptsInfrastructure(configuration)
        .AddPersistenceInfrastructure(configuration);
}
