using Kotlet.Application.Houses;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Houses;

public static class DiExtension
{
    public static IServiceCollection AddHousesInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IHouseRepository, HouseRepository>();
}
