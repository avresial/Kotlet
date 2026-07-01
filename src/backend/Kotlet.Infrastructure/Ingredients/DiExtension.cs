using Kotlet.Application.Ingredients;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Ingredients;

public static class DiExtension
{
    public static IServiceCollection AddIngredientsInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IIngredientRepository, IngredientRepository>();
}
