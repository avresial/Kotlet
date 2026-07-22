using Kotlet.Api.Auth;
using Kotlet.Api.Recipes;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
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
        services.AddMcpServer(options =>
            {
                // Report a stable name and the assembly version (see Kotlet.Api.csproj <Version>)
                // so clients display and cache-key the MCP server correctly; bumping the version
                // invalidates cached connector metadata in clients such as ChatGPT.
                options.ServerInfo = new Implementation
                {
                    Name = "Kotlet",
                    Version = typeof(DiExtension).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"
                };
                options.ServerInstructions =
                """
                Kotlet is a household food app: recipes, prepared meals, a shopping list, a pantry,
                and a meal planner.
                All data is scoped to the authenticated user's household; ingredients live in a catalog
                shared by every household.

                Browsing data: use get_recipes/get_recipe, get_ingredients,
                get_prepared_meals/get_prepared_meal, get_shopping_list, get_pantry, and
                get_meal_plan_overview/get_meal_plan. Search tools return resource links; the singular
                get_* tools and kotlet:// resources return full data.

                Adding a recipe (e.g. one found on the internet): follow the
                kotlet://recipes/new-recipe-guide resource. In short: check for duplicates first with
                check_recipe_exists (source URL and/or title), find every ingredient in one call
                with get_ingredients, create genuinely missing ones after user confirmation, then
                call add_recipe exactly once with title, servings, a Markdown description containing
                numbered steps (cite the source URL when imported), and the ingredient IDs with
                quantities.
                Recipes cannot be edited through MCP.

                Quantities always use the ingredient's base measurement unit (g or ml) unless the tool
                says otherwise. Dates use yyyy-MM-dd.

                In hosts that support MCP Apps, show_recipes renders household recipes as interactive
                cards; other hosts receive a plain text list from it.
                """;
            })
            .WithHttpTransport(options => options.Stateless = true)
            // Tools, prompts, and resources live next to each feature's HTTP endpoints
            // (e.g. Recipes/RecipeMcp.cs). Scan the assembly so new domains register automatically.
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            .WithResourcesFromAssembly();
        // The MCP Apps (SEP-1865) recipe UI primitives carry dynamic _meta.ui metadata, which
        // attribute scanning cannot express, so they are registered as singletons directly.
        services.AddSingleton(RecipeUiMcp.CreateShowRecipesTool);
        services.AddSingleton<McpServerResource>(_ =>
            RecipeUiMcp.CreateRecipesUiResource(RecipeUiMcp.ApiOrigin(oauth)));
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp("/mcp").RequireAuthorization(AuthorizationPolicy);
        endpoints.MapMcpDiscovery();
        return endpoints;
    }
}
