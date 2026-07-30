using Kotlet.Api.Auth;
using Kotlet.Api.MealPlanner;
using Kotlet.Api.Recipes;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using System.Text.Json;

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

                Meal-planning flow:
                1. Resolve all user-supplied ingredient names in one get_ingredients call.
                2. Use get_recipes for compact recipe candidates; filter by ingredientIds to favor
                   shared ingredients. Use get_prepared_meals for compact ready-meal candidates.
                   These compact tools are for reasoning; singular get_* tools/resources return detail.
                3. Compose up to seven days in one add-week request. Repeats are allowed when the user
                   wants them. Prefer candidates sharing ingredients when requested.
                4. Call preview_meal_plan before saving. It validates and renders the draft without
                   changing data. Its request is identical to add_weekly_meal_plan.
                5. Save only after approval by calling add_weekly_meal_plan with the unchanged request.
                   In MCP Apps hosts the preview offers an explicit Add to Kotlet button.

                Other browsing: get_shopping_list, get_pantry, get_meal_plan_overview/get_meal_plan.

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
                cards and show_meal_plan renders a read-only day view of the meal plan; other hosts
                receive a plain text list from them.
                UI-only tools: show_recipes renders recipe cards; show_meal_plan renders a read-only
                day view; preview_meal_plan renders a weekly draft. Use compact get_* tools when UI
                presentation was not requested.
                """;
            })
            .WithHttpTransport(options => options.Stateless = true)
            // Tools, prompts, and resources live next to each feature's HTTP endpoints
            // (e.g. Recipes/RecipeMcp.cs). Scan the assembly so new domains register automatically.
            .WithToolsFromAssembly()
            .WithPromptsFromAssembly()
            .WithResourcesFromAssembly()
            .WithRequestFilters(filters => filters
                .AddListToolsFilter(next => async (request, cancellationToken) =>
                {
                    var result = await next(request, cancellationToken);
                    DataUiMcp.AttachTo(result.Tools);
                    foreach (var tool in result.Tools.Where(tool => tool.OutputSchema is not null && !tool.Name.StartsWith("show_")))
                        tool.OutputSchema = McpToolResultAdapter.OutputSchema(tool.Name)
                            ?? JsonSerializer.SerializeToElement(new { type = "object" });
                    return result;
                })
                .AddCallToolFilter(next => async (request, cancellationToken) =>
            {
                var result = await next(request, cancellationToken);
                if (!request.Params.Name.StartsWith("show_") && request.Params.Name != MealPlanUiMcp.ToolName)
                    McpToolResultAdapter.Apply(request.Params.Name, result);
                return result;
            }));
        // The MCP Apps (SEP-1865) recipe UI primitives carry dynamic _meta.ui metadata, which
        // attribute scanning cannot express, so they are registered as singletons directly.
        services.AddSingleton(RecipeUiMcp.CreateShowRecipesTool);
        services.AddSingleton<McpServerResource>(_ =>
            RecipeUiMcp.CreateRecipesUiResource(RecipeUiMcp.ApiOrigin(oauth)));
        services.AddSingleton(MealPlannerUiMcp.CreateShowMealPlanTool);
        services.AddSingleton<McpServerResource>(_ =>
            MealPlannerUiMcp.CreateMealPlanUiResource(MealPlannerUiMcp.ApiOrigin(oauth)));
        services.AddSingleton<McpServerResource>(_ =>
            DataUiMcp.CreateResource(RecipeUiMcp.ApiOrigin(oauth)));
        services.AddSingleton(MealPlanUiMcp.CreatePreviewTool);
        services.AddSingleton<McpServerResource>(_ =>
            MealPlanUiMcp.CreateUiResource(RecipeUiMcp.ApiOrigin(oauth)));
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMcp("/mcp").RequireAuthorization(AuthorizationPolicy);
        endpoints.MapMcpDiscovery();
        return endpoints;
    }
}
