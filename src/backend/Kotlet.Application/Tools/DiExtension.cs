using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Application.Tools;

public static class DiExtension
{
    public static IServiceCollection AddToolsApplication(this IServiceCollection services) => services
        .AddScoped<VideoAnalysisService>()
        .AddSingleton<IVideoAnalysisSignal, VideoAnalysisSignal>();
}
