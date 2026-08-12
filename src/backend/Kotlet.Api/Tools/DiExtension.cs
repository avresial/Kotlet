namespace Kotlet.Api.Tools;

public static class DiExtension
{
    public static IServiceCollection AddToolsFeature(this IServiceCollection services)
    {
        services.AddHostedService<VideoAnalysisWorker>();
        return services;
    }

    public static IEndpointRouteBuilder MapToolsFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapVideoAnalysisEndpoints();
}
