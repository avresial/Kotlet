using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Kotlet.Api.Auth;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Kotlet.Api.Persistence;

public sealed class DatabaseMigrationWorker(
    IServiceScopeFactory scopeFactory,
    IWebHostEnvironment environment,
    IOptions<OAuthOptions> oauthOptions,
    MigrationReadySignal migrationReady,
    ILogger<DatabaseMigrationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Applying database migrations");

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KotletDbContext>();
        if (dbContext.Database.IsSqlite())
            await dbContext.Database.EnsureCreatedAsync(stoppingToken);
        else
            await dbContext.Database.MigrateAsync(stoppingToken);

        var applications = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var oauth = oauthOptions.Value;
        var application = await applications.FindByClientIdAsync(oauth.ClientId, stoppingToken);
        var client = new OpenIddictApplicationDescriptor
        {
            ClientId = oauth.ClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Kotlet MCP client"
        };
        foreach (var redirectUri in oauth.RedirectUris)
            client.RedirectUris.Add(new Uri(redirectUri));
        client.Permissions.UnionWith([
            Permissions.Endpoints.Authorization,
            Permissions.Endpoints.Token,
            Permissions.GrantTypes.AuthorizationCode,
            Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code,
            Permissions.Prefixes.Scope + "mcp",
            Permissions.Prefixes.Resource + oauth.Resource
        ]);
        if (oauth.RequirePkce)
            client.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        if (application is null)
            await applications.CreateAsync(client, stoppingToken);
        else
            await applications.UpdateAsync(application, client, stoppingToken);

        var scopes = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var existingScope = await scopes.FindByNameAsync("mcp", stoppingToken);
        var mcpScope = new OpenIddictScopeDescriptor { Name = "mcp", DisplayName = "Kotlet MCP API" };
        mcpScope.Resources.Add(oauth.Resource);
        if (existingScope is null)
            await scopes.CreateAsync(mcpScope, stoppingToken);
        else
            await scopes.UpdateAsync(existingScope, mcpScope, stoppingToken);

        migrationReady.SetReady();
        logger.LogInformation("Database migrations applied successfully");

        // The integration-test host shares a single in-memory SQLite connection across the
        // background worker and request handlers; SQLite cannot nest transactions, so seeding
        // reference data here races with request-handling writes. Real deployments use a
        // connection-pooled database, so seed everywhere except the Test environment.
        if (!environment.IsEnvironment("Test"))
        {
            var ingredientsSeeder = scope.ServiceProvider.GetRequiredService<IngredientCsvSeeder>();
            await ingredientsSeeder.SeedAsync(stoppingToken);
        }

        if (environment.IsDevelopment())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedAsync(stoppingToken);
        }
    }
}
