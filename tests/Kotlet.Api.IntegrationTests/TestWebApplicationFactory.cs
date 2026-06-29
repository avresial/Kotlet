using Kotlet.Api.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kotlet.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", "Sqlite");
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

    // DatabaseMigrationWorker is a BackgroundService that runs asynchronously after host
    // startup, so tests can race against it. We wait for its completion signal so every
    // test class gets a fully-migrated database before its first request.
    public async Task InitializeAsync()
    {
        var signal = Services.GetRequiredService<MigrationReadySignal>();
        await signal.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(30)).Token);
    }

    public new async Task DisposeAsync() => await base.DisposeAsync();
}
