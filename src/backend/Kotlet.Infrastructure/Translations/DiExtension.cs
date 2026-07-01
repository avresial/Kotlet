using Kotlet.Application.Translations;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Translations;

public static class DiExtension
{
    public static IServiceCollection AddTranslationsInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<ITranslationRepository, TranslationRepository>();
        services.AddScoped<TranslationCacheInterceptor>();
        return services;
    }
}
