using Kotlet.Application.Pantry;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Pantry;

public static class DiExtension
{
    public static IServiceCollection AddPantryInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<IPantryRepository, PantryRepository>();
        services.AddScoped<IPantryRecipeMatchCache, PantryRecipeMatchMemoryCache>();
        services.AddScoped<PantryRecipeMatchCacheInterceptor>();
        return services;
    }
}
