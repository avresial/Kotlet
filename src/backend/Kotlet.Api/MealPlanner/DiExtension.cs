namespace Kotlet.Api.MealPlanner;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapMealPlannerFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMealPlannerEndpoints();
}
