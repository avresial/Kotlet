namespace Kotlet.Api.Admin;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapAdminFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapAdminEndpoints();
}
