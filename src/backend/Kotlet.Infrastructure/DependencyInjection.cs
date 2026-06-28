using Kotlet.Application.MealPlanner;
using Kotlet.Application.Ai;
using Kotlet.Infrastructure.Ai;
using Kotlet.Application.Menu.GetMenu;
using Kotlet.Application.Ingredients;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.MealPlanner;
using Kotlet.Infrastructure.Menu;
using Kotlet.Infrastructure.Ingredients;
using Kotlet.Infrastructure.Persistence;
using Kotlet.Application.Pantry;
using Kotlet.Infrastructure.Pantry;
using Kotlet.Application.Recipes;
using Kotlet.Infrastructure.Recipes;
using Kotlet.Application.Shopping;
using Kotlet.Application.Translations;
using Kotlet.Application.Measurements;
using Kotlet.Infrastructure.Shopping;
using Kotlet.Infrastructure.Translations;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMenuReader, InMemoryMenuReader>();
        services.AddMemoryCache();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IUserAiProviderRepository, UserAiProviderRepository>();
        services.AddScoped<ITranslationRepository, TranslationRepository>();
        services.AddScoped<TranslationCacheInterceptor>();
        services.AddScoped<IPantryRepository, PantryRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRecipeImageRepository, RecipeImageRepository>();
        services.AddSingleton<MeasurementMappingService>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        AddDatabase(services, configuration);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IngredientCsvSeeder>();
        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";

        if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<KotletDbContext>((sp, options) =>
                options.UseNpgsql(
                        configuration.GetConnectionString("kotletdb")
                            ?? throw new InvalidOperationException("Connection string 'kotletdb' is not configured."),
                        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Kotlet))
                    .AddInterceptors(sp.GetRequiredService<TranslationCacheInterceptor>()));
            return;
        }

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = $"Data Source=Kotlet-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAliveConnection = new SqliteConnection(connectionString);
            keepAliveConnection.Open();
            services.AddSingleton(keepAliveConnection);
            services.AddDbContext<KotletDbContext>((sp, options) =>
                options.UseSqlite(connectionString)
                    .AddInterceptors(sp.GetRequiredService<TranslationCacheInterceptor>()));
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider '{provider}'. Use 'PostgreSQL' or 'Sqlite'.");
    }
}
