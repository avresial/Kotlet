using Kotlet.Application.Ai;

namespace Kotlet.Api.Ingredients;

public static class DiExtension
{
    public static IServiceCollection AddIngredientsFeature(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Application-level AI credentials back the translation worker. Bound as a singleton instance so
        // the (config-free) Application layer can depend on ApplicationAiOptions directly.
        var applicationAi = new ApplicationAiOptions();
        configuration.GetSection(ApplicationAiOptions.SectionName).Bind(applicationAi);
        services.AddSingleton(applicationAi);

        services.AddHostedService<IngredientTranslationWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapIngredientsFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapIngredientEndpoints();
}
