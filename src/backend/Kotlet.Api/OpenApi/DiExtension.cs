using Scalar.AspNetCore;

namespace Kotlet.Api.OpenApi;

public static class DiExtension
{
    public static IServiceCollection AddOpenApiFeature(this IServiceCollection services) => services.AddOpenApi();

    public static WebApplication MapOpenApiFeature(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return app;
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "Kotlet API";
            options.Theme = ScalarTheme.Purple;
        });
        return app;
    }
}
