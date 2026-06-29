namespace Kotlet.Api.Houses;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapHousesFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHouseEndpoints();
}
