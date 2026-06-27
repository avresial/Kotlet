using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Kotlet.Infrastructure.Persistence;

namespace Kotlet.Api.IntegrationTests;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
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
            // Remove the PostgreSQL DbContext registration
            services.RemoveAll(typeof(DbContextOptions<KotletDbContext>));

            // Get the configuration from the service provider
            var serviceProvider = services.BuildServiceProvider();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            
            // Register DbContext with SQLite
            services.AddDbContext<KotletDbContext>(options =>
                options.UseSqlite(configuration.GetConnectionString("kotletdb") 
                    ?? throw new InvalidOperationException("Test connection string not configured")));
        });

        builder.UseEnvironment("Test");
    }
}
