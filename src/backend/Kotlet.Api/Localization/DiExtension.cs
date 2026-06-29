namespace Kotlet.Api.Localization;

public static class DiExtension
{
    public static IServiceCollection AddLocalizationFeature(this IServiceCollection services)
    {
        services.AddScoped<ILanguageContext, LanguageContext>();
        return services;
    }
}
