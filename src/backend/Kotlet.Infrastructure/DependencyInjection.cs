using Kotlet.Application.MealPlanner;
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
using Kotlet.Infrastructure.Shopping;
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
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IPantryRepository, PantryRepository>();
        services.AddScoped<IShoppingListRepository, ShoppingListRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IRecipeImageRepository, RecipeImageRepository>();
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
        AddDatabase(services, configuration);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IngredientsSeeder>();
        services.AddScoped<IngredientCsvSeeder>();
        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";

        if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<KotletDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("kotletdb")
                        ?? throw new InvalidOperationException("Connection string 'kotletdb' is not configured."),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Kotlet)));
            return;
        }

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = $"Data Source=Kotlet-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAliveConnection = new SqliteConnection(connectionString);
            keepAliveConnection.Open();
            services.AddSingleton(keepAliveConnection);
            services.AddDbContext<KotletDbContext>(options => options.UseSqlite(connectionString));
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider '{provider}'. Use 'PostgreSQL' or 'Sqlite'.");
    }
}
