using Kotlet.Application.PreparedMeals;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.PreparedMeals;

public static class DiExtension
{
    public static IServiceCollection AddPreparedMealsInfrastructure(this IServiceCollection services) => services.AddScoped<IPreparedMealRepository, PreparedMealRepository>().AddScoped<IPreparedMealImageRepository, PreparedMealImageRepository>();
}
