namespace Kotlet.Api.Houses;

public static class DiExtension
{
    public static IServiceCollection AddHousesFeature(this IServiceCollection services) =>
        services.AddScoped<HouseSessionService>();

    public static IEndpointRouteBuilder MapHousesFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHouseEndpoints();
}
