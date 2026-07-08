using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Pantry;

public static class DiExtension
{
    public static IServiceCollection AddPantryApplication(this IServiceCollection services) =>
        services.AddScoped<PantryService>()
            .AddScoped<PantryRecipeMatchService>();
}
