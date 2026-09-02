using Kotlet.Api.Persistence;
using Kotlet.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kotlet.Api.IntegrationTests;

/// <summary>
/// A host backed by the shared fixture in <see cref="KotletTestData"/>: a populated household
/// with the full ingredient catalogue, recipes, a meal plan, a shopping list, and a pantry.
/// Each instance gets its own copy of the seeded database file, so test classes stay isolated
/// from one another while starting from identical, known data.
/// </summary>
/// <remarks>
/// Use this when a test needs realistic data to exist. Use <see cref="TestWebApplicationFactory"/>
/// when a test wants an empty database and creates exactly what it asserts on.
/// </remarks>
public class SeededWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private string? databasePath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Built before the host starts because ConfigureWebHost cannot await.
        databasePath = TestDatabaseTemplate.CreateCopyAsync().GetAwaiter().GetResult();

        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:kotletdb", TestDatabaseTemplate.ConnectionStringFor(databasePath));
        builder.UseSetting("Jwt:Issuer", "Kotlet.Tests");
        builder.UseSetting("Jwt:Audience", "Kotlet.Tests");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-bytes");
        builder.UseSetting("OAuth:Issuer", "http://localhost/");
        builder.UseSetting("OAuth:Resource", "http://localhost/mcp");
        builder.UseSetting("OAuth:LoginUrl", "http://localhost:4200/login");
        builder.UseSetting("OAuth:ClientId", "kotlet-mcp-tests");
        builder.UseSetting("OAuth:RequirePkce", "true");
        builder.UseSetting("OAuth:RedirectUris:0", "http://127.0.0.1/callback");
        builder.UseEnvironment("Test");
    }

    public async Task InitializeAsync()
    {
        var signal = Services.GetRequiredService<MigrationReadySignal>();
        await signal.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (databasePath is { } path)
        {
            TestDatabaseTemplate.Delete(path);
        }
    }
}
