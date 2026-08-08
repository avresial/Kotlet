using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Application.Recipes;
using Microsoft.Extensions.DependencyInjection;
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
    public const string PresentationDataKey = "kotlet/recipeUi";

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
            UseStructuredContent = true,
            OutputSchema = JsonSerializer.SerializeToElement(new
            {
                type = "object",
                properties = new
                {
                    recipes = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                id = new { type = "string", format = "uuid" },
                                title = new { type = "string" },
                                mealType = new { type = new[] { "string", "null" } },
                                servings = new { type = "integer" },
                                ingredientCount = new { type = "integer" },
                                description = new { type = new[] { "string", "null" } },
                                imageUrl = new { type = new[] { "string", "null" } },
                                isAiAssisted = new { type = "boolean" },
                                updatedAtUtc = new { type = "string", format = "date-time" }
                            },
                            required = new[]
                            {
                                "id", "title", "mealType", "servings", "ingredientCount",
                                "description", "imageUrl", "isAiAssisted", "updatedAtUtc"
                            },
                            additionalProperties = false
                        }
                    },
                    totalCount = new { type = "integer" },
                    page = new { type = "integer" },
                    pageSize = new { type = "integer" },
                    apiOrigin = new { type = "string", format = "uri" }
                },
                required = new[] { "recipes", "totalCount", "page", "pageSize", "apiOrigin" },
                additionalProperties = false
            }, JsonSerializerOptions.Web),
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject { ["resourceUri"] = ResourceUri },
                // ChatGPT's Apps SDK links a tool to its widget through its own metadata
                // namespace rather than _meta.ui.resourceUri; provided alongside so the same
                // tool works in both SEP-1865 MCP Apps hosts and ChatGPT.
                ["openai/outputTemplate"] = ResourceUri,
                ["openai/toolInvocation/invoking"] = "Loading recipes...",
                ["openai/toolInvocation/invoked"] = "Recipes ready"
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
                    ["domain"] = apiOrigin,
                    ["prefersBorder"] = true
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
                ["openai/widgetDomain"] = apiOrigin,
                ["openai/widgetDescription"] =
                    "Interactive recipe cards showing the household's recipes, with actions to open recipe details.",
                ["openai/widgetPrefersBorder"] = true
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
        var frontendOrigin = FrontendOrigin(oauth.Value);
        var cards = result.Items
            .Select(recipe => new RecipeUiCard(
                recipe.Id, recipe.Title, recipe.MealType, recipe.Servings, recipe.IngredientCount,
                SummaryText(recipe.DescriptionMarkdown),
                recipe.FirstImageUrl is null ? null : origin + recipe.FirstImageUrl,
                recipe.IsAiAssisted, recipe.UpdatedAtUtc))
            .ToList();
        RecipeUiDetail? singleRecipe = null;
        if (result.TotalCount == 1 && cards.Count == 1)
        {
            var detail = await service.GetByIdAsync(
                cards[0].Id, RequireHouse(currentUser), cancellationToken);
            if (detail is not null)
            {
                singleRecipe = RecipeUiDetail.From(detail, origin, frontendOrigin);
            }
        }
        var toolResult = new CallToolResult
        {
            Content = [new TextContentBlock { Text = FallbackText(cards, result.TotalCount) }],
            StructuredContent = JsonSerializer.SerializeToElement(
                new RecipeUiListData(cards, result.TotalCount, page, pageSize, origin),
                JsonSerializerOptions.Web)
        };
        SetPresentation(toolResult, new RecipeUiListPresentation(
            cards.Select(RecipeUiPresentationCard.From).ToList(), result.TotalCount, singleRecipe));
        return toolResult;
    }

    /// <summary>Routes recipe search and detail results to the dedicated recipe UI.</summary>
    public static void AttachToRecipeTools(IList<Tool> tools)
    {
        foreach (var tool in tools.Where(tool => tool.Name is "get_recipes" or "get_recipe"))
        {
            tool.Meta ??= new JsonObject();
            tool.Meta["ui"] = new JsonObject { ["resourceUri"] = ResourceUri };
            tool.Meta["openai/outputTemplate"] = ResourceUri;
            tool.Meta["openai/toolInvocation/invoking"] = "Loading recipe...";
            tool.Meta["openai/toolInvocation/invoked"] = "Recipe ready";
        }
    }

    /// <summary>
    /// Adds a bounded presentation payload while preserving the existing agent-facing result
    /// contract for recipe planning and imports.
    /// </summary>
    public static async Task ApplyPresentationAsync(
        string toolName,
        CallToolResult result,
        string apiOrigin,
        string frontendOrigin,
        IServiceProvider? services,
        CancellationToken cancellationToken)
    {
        if (result.IsError is true || result.StructuredContent is not { } structuredContent)
        {
            return;
        }

        if (toolName == "get_recipe")
        {
            var detail = TryDeserialize<RecipeDetailResponse>(structuredContent);
            if (detail is not null)
            {
                SetPresentation(result,
                    new RecipeUiDetailPresentation(RecipeUiDetail.From(detail, apiOrigin, frontendOrigin)));
            }
            return;
        }

        if (toolName != "get_recipes")
        {
            return;
        }

        var search = TryDeserialize<McpRecipeSearchResponse>(structuredContent);
        if (search is null)
        {
            return;
        }

        var cards = search.Recipes
            .Select(recipe => new RecipeUiPresentationCard(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.MealType,
                recipe.Servings,
                recipe.Ingredients.Count,
                ToAbsoluteUrlOrNull(apiOrigin, recipe.ImageUrl),
                CanEdit: true))
            .ToList();
        var service = services?.GetService<RecipeService>();
        var currentUser = services?.GetService<ICurrentUser>();
        RecipeUiDetail? singleRecipe = null;
        if (search.TotalCount == 1 && cards.Count == 1 && service is not null && currentUser is not null)
        {
            var detail = await service.GetByIdAsync(
                cards[0].Id, RequireHouse(currentUser), cancellationToken);
            if (detail is not null)
            {
                singleRecipe = RecipeUiDetail.From(detail, apiOrigin, frontendOrigin);
            }
        }

        SetPresentation(result, new RecipeUiListPresentation(cards, search.TotalCount, singleRecipe));
    }

    public static string ApiOrigin(OAuthOptions oauth) =>
        new Uri(oauth.Resource).GetLeftPart(UriPartial.Authority);

    public static string FrontendOrigin(OAuthOptions oauth)
    {
        var loginUri = new Uri(oauth.LoginUrl);
        var path = loginUri.AbsolutePath.TrimEnd('/');
        const string loginPath = "/login";
        if (path.EndsWith(loginPath, StringComparison.OrdinalIgnoreCase))
        {
            path = path[..^loginPath.Length].TrimEnd('/');
        }

        return loginUri.GetLeftPart(UriPartial.Authority) + path;
    }

    private static string FallbackText(IReadOnlyList<RecipeUiCard> cards, int totalCount)
    {
        if (cards.Count == 0)
        {
            return "No recipes found for this household.";
        }
        var lines = cards.Select((card, index) =>
            $"{index + 1}. {card.Title} — {card.Servings} serving(s), {card.IngredientCount} ingredient(s)"
            + (card.MealType is null ? "" : $", {card.MealType}"));
        return $"Household recipes ({cards.Count} of {totalCount}):\n" + string.Join('\n', lines)
             + "\n\nUse get_recipe with a recipe ID from get_recipes for full details.";
    }

    private static void SetPresentation(CallToolResult result, object presentation)
    {
        result.Meta ??= new JsonObject();
        result.Meta[PresentationDataKey] = JsonSerializer.SerializeToNode(presentation, JsonSerializerOptions.Web);
    }

    private static T? TryDeserialize<T>(JsonElement structuredContent)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(structuredContent.GetRawText(), JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    internal static string ToAbsoluteUrl(string apiOrigin, string url) =>
        url.StartsWith("/", StringComparison.Ordinal) ? apiOrigin + url : url;

    internal static string? ToAbsoluteUrlOrNull(string apiOrigin, string? url) =>
        url is null ? null : ToAbsoluteUrl(apiOrigin, url);

    internal static string? SummaryText(string? description) => McpRecipeText.Summary(description);
}

/// <summary>One recipe card in the embedded MCP App UI.</summary>
public sealed record RecipeUiCard(
    Guid Id,
    string Title,
    string? MealType,
    int Servings,
    int IngredientCount,
    string? Description,
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

/// <summary>Small, user-facing recipe card data used only by the dedicated MCP App.</summary>
public sealed record RecipeUiPresentationCard(
    Guid Id,
    string Title,
    string? Description,
    string? MealType,
    int Servings,
    int IngredientCount,
    string? ImageUrl,
    bool CanEdit)
{
    public static RecipeUiPresentationCard From(RecipeUiCard card) => new(
        card.Id, card.Title,
        RecipeUiMcp.SummaryText(card.Description), card.MealType, card.Servings,
        card.IngredientCount, card.ImageUrl, CanEdit: false);
}

/// <summary>Search result data consumed by the dedicated MCP App.</summary>
public sealed record RecipeUiListPresentation(
    IReadOnlyList<RecipeUiPresentationCard> Recipes,
    int TotalCount,
    RecipeUiDetail? Detail = null)
{
    public string Kind => "list";
}

/// <summary>Detail result wrapper consumed by the dedicated MCP App.</summary>
public sealed record RecipeUiDetailPresentation(RecipeUiDetail Detail)
{
    public string Kind => "detail";
}

/// <summary>Presentation-only recipe detail; persistence and audit fields are intentionally absent.</summary>
public sealed record RecipeUiDetail(
    Guid Id,
    string Title,
    string? Description,
    int Servings,
    string? MealType,
    RecipeUiImage? Image,
    IReadOnlyList<RecipeUiIngredient> Ingredients,
    bool CanEdit,
    bool IsIncomplete,
    string? EditUrl)
{
    public static RecipeUiDetail From(
        RecipeDetailResponse response,
        string apiOrigin,
        string frontendOrigin)
    {
        var ingredients = response.Ingredients
            .Select(RecipeUiIngredient.From)
            .ToList();
        var image = response.Images
            .OrderBy(image => image.SortOrder)
            .Select(image =>
            {
                var contentUrl = image.ContentUrl;
                return contentUrl is null
                    ? null
                    : new RecipeUiImage(
                        RecipeUiMcp.ToAbsoluteUrl(apiOrigin, contentUrl), image.AltText ?? response.Title);
            })
            .OfType<RecipeUiImage>()
            .FirstOrDefault();
        var isIncomplete = ingredients.Count == 0 || string.IsNullOrWhiteSpace(response.DescriptionMarkdown);
        return new(
            response.Id,
            response.Title,
            response.DescriptionMarkdown,
            response.Servings,
            response.MealType,
            image,
            ingredients,
            response.CanEdit,
            isIncomplete,
            response.CanEdit ? $"{frontendOrigin}/recipes/{response.Id}/edit" : null);
    }
}

/// <summary>Image data needed to render a recipe hero image, without storage metadata.</summary>
public sealed record RecipeUiImage(string Url, string? AltText);

/// <summary>Ingredient data needed to render a recipe, without catalog or storage metadata.</summary>
public sealed record RecipeUiIngredient(string Name, decimal Quantity, string Unit, string? Note)
{
    public static RecipeUiIngredient From(RecipeIngredientResponse ingredient) =>
        new(ingredient.Name, ingredient.Quantity, ingredient.Unit, ingredient.Note);
}
