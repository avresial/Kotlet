using Kotlet.Application.Menu.GetMenu;
using Kotlet.Infrastructure.Menu;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Replace with a Supabase-backed implementation when database credentials are configured.
        services.AddSingleton<IMenuReader, InMemoryMenuReader>();
        return services;
    }
}
