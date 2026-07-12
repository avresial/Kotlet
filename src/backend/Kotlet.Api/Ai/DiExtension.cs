namespace Kotlet.Api.Ai;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapAiFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapAiProviderEndpoints();
        endpoints.MapAiTranslationEndpoints();
        endpoints.MapAgentEndpoints();
        return endpoints;
    }
}
