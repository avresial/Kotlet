using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Houses;

public static class DiExtension
{
    public static IServiceCollection AddHousesApplication(this IServiceCollection services) =>
        services.AddScoped<HouseService>();
}
