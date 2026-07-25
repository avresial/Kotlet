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
    IConfiguration configuration,
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

        await SeedAsync(scope, dbContext, stoppingToken);
    }

    /// <summary>
    /// Populates reference and development data. The shared sample fixture supersedes both the
    /// plain ingredient seed and the development user seed rather than running alongside them:
    /// it creates the same accounts and the same catalogue, but with the deterministic ids the
    /// benchmark and the browser tests rely on. Running the others first would leave the
    /// catalogue full of random ids and the default household already created, at which point
    /// the fixture finds its work done and seeds nothing.
    /// </summary>
    private async Task SeedAsync(IServiceScope scope, KotletDbContext dbContext, CancellationToken stoppingToken)
    {
        if (SampleDataRequested())
        {
            await SeedSampleDataAsync(dbContext, stoppingToken);
            return;
        }

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

    private bool SampleDataRequested() => configuration.GetValue("Database:SeedSampleData", false);

#if SAMPLE_DATA
    /// <summary>
    /// Loads the fixture the MCP benchmark and the browser tests share, so a local run starts
    /// with recipes, a meal plan, a shopping list, and a pantry instead of an empty household.
    /// </summary>
    private async Task SeedSampleDataAsync(KotletDbContext dbContext, CancellationToken stoppingToken)
    {
        logger.LogInformation("Seeding the shared sample dataset");
        await Kotlet.TestData.KotletTestData.SeedAsync(dbContext, stoppingToken);
    }
#else
    /// <summary>
    /// Release builds do not reference the test-data fixture, so the sample dataset is
    /// unavailable. Say so rather than starting with an unexpectedly empty database.
    /// </summary>
    private Task SeedSampleDataAsync(KotletDbContext dbContext, CancellationToken stoppingToken)
    {
        logger.LogWarning(
            "Database:SeedSampleData is set, but this is a Release build and the sample dataset " +
            "is only compiled into development builds. No data was seeded.");
        return Task.CompletedTask;
    }
#endif
}
