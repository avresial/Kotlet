using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kotlet.Infrastructure.Pantry;
using Kotlet.Infrastructure.Translations;

namespace Kotlet.Infrastructure.Persistence;

public static class DiExtension
{
    public static IServiceCollection AddPersistenceInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        AddDatabase(services, configuration);
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IngredientCsvSeeder>();
        return services;
    }

    private static void AddDatabase(IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "PostgreSQL";

        if (provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<KotletDbContext>((serviceProvider, options) =>
                options.UseNpgsql(
                        configuration.GetConnectionString("kotletdb")
                            ?? throw new InvalidOperationException("Connection string 'kotletdb' is not configured."),
                        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", DatabaseSchemas.Kotlet))
                    .UseOpenIddict()
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<TranslationCacheInterceptor>(),
                        serviceProvider.GetRequiredService<PantryRecipeMatchCacheInterceptor>()));
            return;
        }

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = $"Data Source=Kotlet-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
            var keepAliveConnection = new SqliteConnection(connectionString);
            keepAliveConnection.Open();
            services.AddSingleton(keepAliveConnection);
            services.AddDbContext<KotletDbContext>((serviceProvider, options) =>
                options.UseSqlite(connectionString)
                    .UseOpenIddict()
                    .AddInterceptors(
                        serviceProvider.GetRequiredService<TranslationCacheInterceptor>(),
                        serviceProvider.GetRequiredService<PantryRecipeMatchCacheInterceptor>()));
            return;
        }

        throw new InvalidOperationException(
            $"Unsupported database provider '{provider}'. Use 'PostgreSQL' or 'Sqlite'.");
    }
}
