namespace Kotlet.Api.Pantry;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapPantryFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPantryEndpoints();
}
