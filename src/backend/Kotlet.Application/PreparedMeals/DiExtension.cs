using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.PreparedMeals;

public static class DiExtension
{
    public static IServiceCollection AddPreparedMealsApplication(this IServiceCollection services) => services.AddScoped<PreparedMealService>().AddScoped<PreparedMealImageService>();
}
