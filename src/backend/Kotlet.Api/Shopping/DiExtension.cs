namespace Kotlet.Api.Shopping;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapShoppingFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapShoppingListEndpoints();
}
