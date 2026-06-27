using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.Api.Persistence;

public sealed class DatabaseMigrationWorker(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    ILogger<DatabaseMigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Applying database migrations");

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KotletDbContext>();
        await dbContext.Database.MigrateAsync(stoppingToken);

        logger.LogInformation("Database migrations applied successfully");

        if (environment.IsDevelopment())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(stoppingToken);
        }
    }
}
