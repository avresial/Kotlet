using Kotlet.Application.VideoTranscripts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.VideoTranscripts;

public static class DiExtension
{
    public static IServiceCollection AddVideoTranscriptsInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = new SupadataOptions();
        configuration.GetSection(SupadataOptions.SectionName).Bind(options);
        services.AddSingleton(options);

        services.AddHttpClient<SupadataVideoTranscriptProvider>(client =>
        {
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.BaseUrl)
                ? SupadataOptions.DefaultBaseUrl
                : options.BaseUrl.TrimEnd('/') + "/");
        });

        services.AddScoped<IVideoTranscriptProvider>(sp =>
            sp.GetRequiredService<SupadataVideoTranscriptProvider>());
        return services;
    }
}
