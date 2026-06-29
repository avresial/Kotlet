namespace Kotlet.Api.Recipes;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapRecipesFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapRecipeEndpoints();
}
