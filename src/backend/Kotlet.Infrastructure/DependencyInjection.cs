using Kotlet.Infrastructure.Admin;
using Kotlet.Infrastructure.Products;
using Kotlet.Application.Products;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
using Kotlet.Infrastructure.Tools;
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
        services.AddScoped<IProductRepository, ProductRepository>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IOpenFoodFactsClient, OpenFoodFactsClient>();
        services.AddHttpClient(OpenFoodFactsClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://world.openfoodfacts.org/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Kotlet/1.0 (+https://github.com/avresial/Kotlet)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddSingleton<IFoodDataCentralClient, FoodDataCentralClient>();
        services.AddHttpClient(FoodDataCentralClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.nal.usda.gov/fdc/v1/");
            client.Timeout = TimeSpan.FromSeconds(8);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Kotlet/1.0 (+https://github.com/avresial/Kotlet)");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddPreparedMealsInfrastructure();

        services.AddRecipesInfrastructure();
        services.AddRecipeImageSearchInfrastructure(configuration);
        services.AddShoppingInfrastructure();
        services.AddTranslationsInfrastructure();
        services.AddToolsInfrastructure();
        services.AddVideoTranscriptsInfrastructure(configuration);
        services.AddPersistenceInfrastructure(configuration);

        return services;
    }
}
