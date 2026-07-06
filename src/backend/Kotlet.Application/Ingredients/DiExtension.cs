using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Ingredients;

public static class DiExtension
{
    public static IServiceCollection AddIngredientsApplication(this IServiceCollection services) => services
        .AddScoped<IngredientService>()
        .AddScoped<IngredientTranslationService>()
        // The signal bridges request-scoped writes and the singleton worker, so it must be a singleton.
        .AddSingleton<IIngredientTranslationSignal, IngredientTranslationSignal>();
}
