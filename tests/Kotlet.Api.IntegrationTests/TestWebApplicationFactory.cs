using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Kotlet.Api.Persistence;
using Kotlet.Infrastructure.Persistence;

namespace Kotlet.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = CreateConnection();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration with test settings
            var testConfig = new Dictionary<string, string?>
            {
                { "ConnectionStrings:kotletdb", "Data Source=:memory:" }
            };
            config.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove the PostgreSQL context configuration and provider services.
            services.RemoveAll<KotletDbContext>();
            services.RemoveAll<DbContextOptions<KotletDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<KotletDbContext>>();
            services.RemoveAll<IDatabaseProvider>();
            var migrationWorker = services.Single(descriptor =>
                descriptor.ServiceType == typeof(IHostedService) &&
                descriptor.ImplementationType == typeof(DatabaseMigrationWorker));
            services.Remove(migrationWorker);
            
            // Register DbContext with SQLite
            services.AddDbContext<KotletDbContext>(options =>
                options.UseSqlite(_connection));
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<KotletDbContext>().Database.EnsureCreated();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    private static SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }
}
