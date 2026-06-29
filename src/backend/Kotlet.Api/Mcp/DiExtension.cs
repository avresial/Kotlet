using Kotlet.Api.Auth;
using ModelContextProtocol.AspNetCore.Authentication;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;

namespace Kotlet.Api.Mcp;

public static class DiExtension
{
    private const string AuthenticationScheme = "McpOAuth";
    private const string AuthorizationPolicy = "Mcp";

    public static IServiceCollection AddMcpFeature(this IServiceCollection services, IConfiguration configuration)
    {
        var oauth = configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>()
            ?? throw new InvalidOperationException("OAuth configuration is missing.");
        services.AddAuthentication()
            .AddPolicyScheme(AuthenticationScheme, "MCP OAuth", options =>
            {
                options.ForwardAuthenticate = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.ForwardChallenge = McpAuthenticationDefaults.AuthenticationScheme;
                options.ForwardForbid = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            })
            .AddMcp(options => options.ResourceMetadata = new()
            {
                AuthorizationServers = { oauth.Issuer },
                ScopesSupported = ["mcp"]
            });
        services.AddAuthorization(options => options.AddPolicy(AuthorizationPolicy, policy =>
        {
            policy.AddAuthenticationSchemes(AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(context => context.User.HasScope("mcp"));
        }));
        services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithTools<KotletReadTools>()
            .WithTools<KotletWriteTools>()
            .WithResources<KotletResources>();
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp("/mcp").RequireAuthorization(AuthorizationPolicy);
        return endpoints;
    }
}
