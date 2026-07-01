using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Ingredients;

public static class DiExtension
{
    public static IServiceCollection AddIngredientsApplication(this IServiceCollection services) =>
        services.AddScoped<IngredientService>();
}
