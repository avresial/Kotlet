namespace Kotlet.Api.Ingredients;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapIngredientsFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapIngredientEndpoints();
}
