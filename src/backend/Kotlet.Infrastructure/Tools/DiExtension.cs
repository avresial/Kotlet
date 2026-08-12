using Kotlet.Application.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Infrastructure.Tools;

public static class DiExtension
{
    public static IServiceCollection AddToolsInfrastructure(this IServiceCollection services) =>
        services.AddScoped<IVideoAnalysisJobRepository, VideoAnalysisJobRepository>();
}
