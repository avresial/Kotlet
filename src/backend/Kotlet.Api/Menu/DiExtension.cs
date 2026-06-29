using Kotlet.Application.Menu.GetMenu;

namespace Kotlet.Api.Menu;

public static class DiExtension
{
    public static IEndpointRouteBuilder MapMenuFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/menu", async (GetMenuQueryHandler handler, CancellationToken cancellationToken) =>
            Results.Ok(await handler.Handle(new GetMenuQuery(), cancellationToken)))
            .WithName("GetMenu");
        return endpoints;
    }
}
