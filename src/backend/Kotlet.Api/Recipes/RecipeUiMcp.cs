using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kotlet.Api.Auth;
using Kotlet.Application.Recipes;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.Recipes;

/// <summary>
/// MCP Apps (SEP-1865) proof of concept: a recipe-card UI rendered inside compatible MCP hosts.
/// The <c>show_recipes</c> tool returns recipe data as structured content and advertises the
/// <c>ui://kotlet/recipes-v2</c> HTML resource through <c>_meta.ui.resourceUri</c>; the embedded UI
/// then calls the existing <c>get_recipe</c> tool through the MCP Apps bridge for details.
/// Registered manually instead of via attribute scanning because both primitives carry
/// dynamic <c>_meta.ui</c> metadata (the CSP resource domain depends on the API origin).
/// </summary>
public static class RecipeUiMcp
{
    public const string ToolName = "show_recipes";
    public const string ResourceUri = "ui://kotlet/recipes-v2";
    public const string ResourceMimeType = "text/html;profile=mcp-app";

    private static readonly Lazy<string> AppHtml = new(() =>
    {
        var assembly = typeof(RecipeUiMcp).Assembly;
        const string name = "Kotlet.Api.Recipes.RecipeUiApp.html";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static McpServerTool CreateShowRecipesTool(IServiceProvider services) =>
        McpServerTool.Create(ShowRecipes, new McpServerToolCreateOptions
        {
            Services = services,
            Name = ToolName,
            Title = "Show recipe cards",
            ReadOnly = true,
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject { ["resourceUri"] = ResourceUri },
                // ChatGPT's Apps SDK links a tool to its widget through its own metadata
                // namespace rather than _meta.ui.resourceUri; provided alongside so the same
                // tool works in both SEP-1865 MCP Apps hosts and ChatGPT.
                ["openai/outputTemplate"] = ResourceUri
            }
        });

    public static McpServerResource CreateRecipesUiResource(string apiOrigin) =>
        McpServerResource.Create(() => AppHtml.Value, new McpServerResourceCreateOptions
        {
            UriTemplate = ResourceUri,
            Name = "recipes-ui",
            Title = "Kotlet recipe cards",
            Description = "Interactive recipe-card UI rendered by MCP hosts that support MCP Apps.",
            MimeType = ResourceMimeType,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject
                {
                    // The iframe CSP the host enforces. Recipe images are served by this API
                    // (anonymous content endpoint), so the API origin must be allowed as a
                    // static-resource source; the UI makes no fetch/XHR/WebSocket or nested-frame
                    // calls, so connectDomains and frameDomains stay empty.
                    ["csp"] = new JsonObject
                    {
                        ["connectDomains"] = new JsonArray(),
                        ["resourceDomains"] = new JsonArray(apiOrigin),
                        ["frameDomains"] = new JsonArray()
                    },
                    // Required by ChatGPT for plugin submission. The host derives a unique
                    // web-sandbox origin from this application-owned HTTPS origin.
                    ["domain"] = apiOrigin
                },
                // ChatGPT's Apps SDK reads the same CSP/domain info from its own (snake_case)
                // metadata namespace, provided alongside _meta.ui so the widget is recognized in
                // ChatGPT as well as in SEP-1865 MCP Apps hosts. widgetDomain is the origin the
                // widget loads static resources (images) from.
                ["openai/widgetCSP"] = new JsonObject
                {
                    ["connect_domains"] = new JsonArray(),
                    ["resource_domains"] = new JsonArray(apiOrigin)
                },
                ["openai/widgetDomain"] = apiOrigin
            }
        });

    [Description("Shows household recipes as interactive cards in MCP hosts that support MCP Apps. " +
                 "Hosts without MCP Apps support receive a plain text list instead. " +
                 "Use get_recipes/get_recipe when you only need recipe data.")]
    private static async Task<CallToolResult> ShowRecipes(
        RecipeService service,
        ICurrentUser currentUser,
        IOptions<OAuthOptions> oauth,
        [Description("Page number, starting at 1.")] int page = 1,
        [Description("Recipes per page, from 1 to 100.")] int pageSize = 12,
        [Description("Optional text to search for in recipes.")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await service.ListAsync(
            RequireHouse(currentUser), page, pageSize, search, null, null, cancellationToken);
        var origin = ApiOrigin(oauth.Value);
        var cards = result.Items
            .Select(recipe => new RecipeUiCard(
                recipe.Id, recipe.Title, recipe.MealType, recipe.Servings, recipe.IngredientCount,
                recipe.FirstImageUrl is null ? null : origin + recipe.FirstImageUrl,
                recipe.IsAiAssisted, recipe.UpdatedAtUtc))
            .ToList();
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = FallbackText(cards, result.TotalCount) }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new RecipeUiListData(cards, result.TotalCount, page, pageSize, origin),
                JsonSerializerOptions.Web)
        };
    }

    public static string ApiOrigin(OAuthOptions oauth) =>
        new Uri(oauth.Resource).GetLeftPart(UriPartial.Authority);

    private static string FallbackText(IReadOnlyList<RecipeUiCard> cards, int totalCount)
    {
        if (cards.Count == 0)
            return "No recipes found for this household.";
        var lines = cards.Select((card, index) =>
            $"{index + 1}. {card.Title} — {card.Servings} serving(s), {card.IngredientCount} ingredient(s)"
            + (card.MealType is null ? "" : $", {card.MealType}"));
        return $"Household recipes ({cards.Count} of {totalCount}):\n" + string.Join('\n', lines)
             + "\n\nUse get_recipe with a recipe ID from get_recipes for full details.";
    }
}

/// <summary>One recipe card in the embedded MCP App UI.</summary>
public sealed record RecipeUiCard(
    Guid Id,
    string Title,
    string? MealType,
    int Servings,
    int IngredientCount,
    string? ImageUrl,
    bool IsAiAssisted,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Structured content of the <c>show_recipes</c> tool, consumed by the embedded UI.</summary>
public sealed record RecipeUiListData(
    IReadOnlyList<RecipeUiCard> Recipes,
    int TotalCount,
    int Page,
    int PageSize,
    string ApiOrigin);
