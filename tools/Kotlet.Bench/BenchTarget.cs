using System.Net;
using Kotlet.Api.Persistence;
using Kotlet.TestData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kotlet.Bench;

/// <summary>The server a benchmark run measures: either booted in this process or already deployed.</summary>
public abstract class BenchTarget : IAsyncDisposable
{
    public abstract string Mode { get; }
    public abstract string Resource { get; }
    public abstract string ClientId { get; }

    /// <summary>Non-null only when the server runs in this process and its queries can be observed.</summary>
    public virtual DbQueryCounter? QueryCounter => null;

    public abstract HttpClient CreateClient();
    public abstract ValueTask DisposeAsync();

    public static async Task<BenchTarget> CreateAsync(BenchOptions options)
    {
        if (options.Url is null)
            return await InProcessTarget.StartAsync();
        return new RemoteTarget(options.Url, options.ClientId ?? "kotlet-mcp-bench");
    }
}

/// <summary>
/// Boots the real API in this process on in-memory SQLite. Nothing external is needed, the
/// database starts empty, and EF commands are observable — which makes byte counts and query
/// counts reproducible run to run.
/// </summary>
public sealed class InProcessTarget : BenchTarget
{
    private readonly Factory factory;
    private readonly DbQueryCounter counter;
    private readonly string databasePath;

    private InProcessTarget(Factory factory, DbQueryCounter counter, string databasePath)
    {
        this.factory = factory;
        this.counter = counter;
        this.databasePath = databasePath;
    }

    public override string Mode => "in-process";
    public override string Resource => "http://localhost/mcp";
    public override string ClientId => "kotlet-mcp-bench";
    public override DbQueryCounter? QueryCounter => counter;

    public static async Task<InProcessTarget> StartAsync()
    {
        // WebApplicationFactory derives the content root from a manifest that only test
        // projects emit, so point the API at its own project directory before it boots.
        Environment.SetEnvironmentVariable("ASPNETCORE_CONTENTROOT", RepoPaths.ApiProject);

        // A private copy of the shared fixture: same rows every run, including the full
        // ingredient catalogue, so payload sizes and query counts are comparable run to run.
        var databasePath = await TestDatabaseTemplate.CreateCopyAsync();

        // Subscribing before the host starts catches the DiagnosticListener as EF creates it.
        var counter = DbQueryCounter.Start();
        var factory = new Factory(TestDatabaseTemplate.ConnectionStringFor(databasePath));
        // Schema checks and OAuth client registration run as a background service, so the first
        // measured call would otherwise race them and absorb their cost.
        var signal = factory.Services.GetRequiredService<MigrationReadySignal>();
        await signal.WaitAsync(new CancellationTokenSource(TimeSpan.FromSeconds(60)).Token);
        return new InProcessTarget(factory, counter, databasePath);
    }

    public override HttpClient CreateClient() =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public override async ValueTask DisposeAsync()
    {
        counter.Dispose();
        await factory.DisposeAsync();
        TestDatabaseTemplate.Delete(databasePath);
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:kotletdb", connectionString);
            // Background workers (translation backfill, recipe import) issue their own queries on
            // their own schedule. They are not part of the MCP request path, and leaving them on
            // would leak queries into whichever call happened to be running. Schema creation and
            // seeding stay, because the measured calls need a populated database.
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(service => service.ServiceType == typeof(IHostedService)
                                               && service.ImplementationType != typeof(DatabaseMigrationWorker))
                             .ToArray())
                    services.Remove(descriptor);
            });
            builder.ConfigureLogging(logging => logging.ClearProviders());

            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("Jwt:Issuer", "Kotlet.Bench");
            builder.UseSetting("Jwt:Audience", "Kotlet.Bench");
            builder.UseSetting("Jwt:SigningKey", "mcp-benchmark-signing-key-at-least-32-bytes");
            builder.UseSetting("OAuth:Issuer", "http://localhost/");
            builder.UseSetting("OAuth:Resource", "http://localhost/mcp");
            builder.UseSetting("OAuth:LoginUrl", "http://localhost:4200/login");
            builder.UseSetting("OAuth:ClientId", "kotlet-mcp-bench");
            builder.UseSetting("OAuth:RequirePkce", "true");
            builder.UseSetting("OAuth:RedirectUris:0", "http://127.0.0.1/callback");
            builder.UseEnvironment("Test");
        }
    }
}

/// <summary>
/// Measures a deployed instance over the network, so the numbers include real transport and
/// real database latency. Query counts are unavailable from outside the process.
/// </summary>
public sealed class RemoteTarget(Uri baseAddress, string clientId) : BenchTarget
{
    public override string Mode => $"remote ({baseAddress})";
    public override string Resource => new Uri(baseAddress, "/mcp").AbsoluteUri;
    public override string ClientId => clientId;

    public override HttpClient CreateClient() =>
        new(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(120)
        };

    public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
