using Kotlet.Api.Admin;
using Kotlet.Api.Ai;
using Kotlet.Api.FoodSettings;
using Kotlet.Api.Auth;
using Kotlet.Api.Houses;
using Kotlet.Api.Ingredients;
using Kotlet.Api.Localization;
using Kotlet.Api.Mcp;
using Kotlet.Api.MealPlanner;
using Kotlet.Api.OpenApi;
using Kotlet.Api.Pantry;
using Kotlet.Api.PreparedMeals;
using Kotlet.Api.Persistence;
using Kotlet.Api.Recipes;
using Kotlet.Api.Shopping;
using System.Threading.RateLimiting;

namespace Kotlet.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddOpenApiFeature();
        services.AddAuthFeature(configuration, environment);
        services.AddHousesFeature();
        services.AddMcpFeature(configuration);
        services.AddLocalizationFeature();
        services.AddIngredientsFeature(configuration);
        services.AddRecipesFeature();
        services.AddPersistenceFeature();
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
        // Bound the anonymous, open Dynamic Client Registration endpoint: each successful call persists a
        // new OpenIddict application row, so throttle per client IP to blunt scripted/spam registration.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(OAuthRegistrationEndpoints.RateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });
        return services;
    }

    public static WebApplication MapApiFeatures(this WebApplication app)
    {
        app.MapOpenApiFeature();
        app.MapAuthFeature();
        app.MapMcpFeature();
        app.MapAiFeature();
        app.MapFoodSettingsEndpoints();
        app.MapHousesFeature();
        app.MapAdminFeature();
        app.MapIngredientsFeature();
        app.MapPantryFeature();
        app.MapPreparedMealEndpoints();
        app.MapShoppingFeature();
        app.MapRecipesFeature();
        app.MapMealPlannerFeature();
        return app;
    }
}
