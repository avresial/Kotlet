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
        services.AddMcpServer(options => options.ServerInstructions =
                """
                Kotlet is a household food app: recipes, a shopping list, a pantry, and a meal planner.
                All data is scoped to the authenticated user's household; ingredients live in a catalog
                shared by every household.

                Browsing data: use get_recipes/get_recipe, get_ingredients/get_ingredient,
                get_shopping_list, get_pantry, and get_meal_plan_overview/get_meal_plan. Search tools
                return resource links; the singular get_* tools and kotlet:// resources return full data.

                Adding a recipe (e.g. one found on the internet): follow the
                kotlet://recipes/new-recipe-guide resource. In short: check for duplicates first with
                check_recipe_exists (source URL and/or title), resolve every ingredient in one call
                with resolve_ingredients_batch (createMissing true creates genuinely new ones), then
                call add_recipe exactly once with title, servings, a Markdown description containing
                numbered steps (cite the source URL when imported), and the ingredient IDs with
                quantities. get_ingredients and create_ingredient remain available for single lookups.
                Recipes cannot be edited through MCP.

                Quantities always use the ingredient's base measurement unit (g or ml) unless the tool
                says otherwise. Dates use yyyy-MM-dd.
                """)
            .WithHttpTransport(options => options.Stateless = true)
            // Tools, prompts, and resources live next to each feature's HTTP endpoints
            // (e.g. Recipes/RecipeMcp.cs). Scan the assembly so new domains register automatically.
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            .WithResourcesFromAssembly();
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp("/mcp").RequireAuthorization(AuthorizationPolicy);
        endpoints.MapMcpDiscovery();
        return endpoints;
    }
}
