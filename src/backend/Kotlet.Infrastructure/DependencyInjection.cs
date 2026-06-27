using Kotlet.Application.Menu.GetMenu;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Menu;
using Kotlet.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IMenuReader, InMemoryMenuReader>();
        services.AddDbContext<KotletDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("kotletdb")
                ?? throw new InvalidOperationException("Connection string 'kotletdb' is not configured.")));
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseSeeder>();
        return services;
    }
}
