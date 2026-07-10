namespace Kotlet.Api.Recipes;

public static class DiExtension
{
    public static IServiceCollection AddRecipesFeature(this IServiceCollection services)
    {
        services.AddHostedService<RecipeImportWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapRecipesFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapRecipeEndpoints();
}
