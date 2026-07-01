using Kotlet.Application.MealPlanner;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.MealPlanner;

public static class DiExtension
{
    public static IServiceCollection AddMealPlannerInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IMealPlanRepository, MealPlanRepository>();
}
