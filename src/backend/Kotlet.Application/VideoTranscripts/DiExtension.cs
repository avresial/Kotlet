using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.VideoTranscripts;

public static class DiExtension
{
    public static IServiceCollection AddVideoTranscriptsApplication(this IServiceCollection services) =>
        services.AddScoped<VideoTranscriptService>();
}
