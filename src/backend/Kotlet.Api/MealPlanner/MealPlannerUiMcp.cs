using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kotlet.Api.Auth;
using Kotlet.Application.MealPlanner;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.MealPlanner;

/// <summary>
/// MCP Apps (SEP-1865) proof of concept: a read-only day view of the household meal planner,
/// rendered inside compatible MCP hosts. The <c>show_meal_plan</c> tool returns one day's plan as
/// structured content and advertises the <c>ui://kotlet/meal-plan-v1</c> HTML resource through
/// <c>_meta.ui.resourceUri</c>. The embedded UI mirrors the Angular meal-planner component's look
/// (slots, meal items, participant portions, day summary) but strips every mutating control — no
/// add/remove, drag-and-drop, or editable portion fields — so it stays a safe, read-only surface.
/// The UI re-calls <c>show_meal_plan</c> through the MCP Apps bridge to page between days.
/// Registered manually instead of via attribute scanning because both primitives carry dynamic
/// <c>_meta.ui</c> metadata (the CSP resource domain depends on the API origin).
/// </summary>
public static class MealPlannerUiMcp
{
    public const string ToolName = "show_meal_plan";
    public const string ResourceUri = "ui://kotlet/meal-plan-v1";
    public const string ResourceMimeType = "text/html;profile=mcp-app";

    // Slot order and labels mirror the Angular meal-planner-page component (slots array and
    // slotLabels), so the embedded read-only view lists slots the same way as the app.
    private static readonly (string Slot, string Label)[] Slots =
    [
        ("breakfast", "Breakfast"),
        ("second-breakfast", "Second breakfast"),
        ("dinner", "Lunch"),
        ("snack", "Snack"),
        ("supper", "Dinner")
    ];

    private static readonly Lazy<string> AppHtml = new(() =>
    {
        var assembly = typeof(MealPlannerUiMcp).Assembly;
        const string name = "Kotlet.Api.MealPlanner.MealPlannerUiApp.html";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static McpServerTool CreateShowMealPlanTool(IServiceProvider services) =>
        McpServerTool.Create(ShowMealPlan, new McpServerToolCreateOptions
        {
            Services = services,
            Name = ToolName,
            Title = "Show meal plan",
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
                    date = new { type = "string", format = "date" },
                    slots = new
                    {
                        type = "array",
                        items = new
                        {
                            type = "object",
                            properties = new
                            {
                                slot = new { type = "string" },
                                label = new { type = "string" },
                                items = new
                                {
                                    type = "array",
                                    items = new
                                    {
                                        type = "object",
                                        properties = new
                                        {
                                            id = new { type = "string", format = "uuid" },
                                            type = new { type = "string" },
                                            displayName = new { type = "string" },
                                            note = new { type = new[] { "string", "null" } },
                                            servings = new { type = "number" },
                                            guests = new { type = "integer" },
                                            ingredientQuantity = new { type = new[] { "number", "null" } },
                                            ingredientUnit = new { type = new[] { "string", "null" } },
                                            participants = new
                                            {
                                                type = "array",
                                                items = new
                                                {
                                                    type = "object",
                                                    properties = new
                                                    {
                                                        displayName = new { type = "string" },
                                                        isCurrentUser = new { type = "boolean" },
                                                        portionPercent = new { type = "integer" }
                                                    },
                                                    required = new[] { "displayName", "isCurrentUser", "portionPercent" },
                                                    additionalProperties = false
                                                }
                                            }
                                        },
                                        required = new[]
                                        {
                                            "id", "type", "displayName", "note", "servings", "guests",
                                            "ingredientQuantity", "ingredientUnit", "participants"
                                        },
                                        additionalProperties = false
                                    }
                                }
                            },
                            required = new[] { "slot", "label", "items" },
                            additionalProperties = false
                        }
                    },
                    mealCount = new { type = "integer" }
                },
                required = new[] { "date", "slots", "mealCount" },
                additionalProperties = false
            }, JsonSerializerOptions.Web),
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject { ["resourceUri"] = ResourceUri },
                // ChatGPT's Apps SDK links a tool to its widget through its own metadata
                // namespace rather than _meta.ui.resourceUri; provided alongside so the same
                // tool works in both SEP-1865 MCP Apps hosts and ChatGPT.
                ["openai/outputTemplate"] = ResourceUri,
                ["openai/toolInvocation/invoking"] = "Loading meal plan...",
                ["openai/toolInvocation/invoked"] = "Meal plan ready"
            }
        });

    public static McpServerResource CreateMealPlanUiResource(string apiOrigin) =>
        McpServerResource.Create(() => AppHtml.Value, new McpServerResourceCreateOptions
        {
            UriTemplate = ResourceUri,
            Name = "meal-plan-ui",
            Title = "Kotlet meal plan (read-only)",
            Description = "Read-only day view of the household meal plan, rendered by MCP hosts that support MCP Apps.",
            MimeType = ResourceMimeType,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject
                {
                    // The read-only view renders only text delivered as structured content: no images,
                    // fetch/XHR/WebSocket, or nested frames, so every CSP domain list stays empty.
                    ["csp"] = new JsonObject
                    {
                        ["connectDomains"] = new JsonArray(),
                        ["resourceDomains"] = new JsonArray(),
                        ["frameDomains"] = new JsonArray()
                    },
                    // Required by ChatGPT for plugin submission. The host derives a unique
                    // web-sandbox origin from this application-owned HTTPS origin.
                    ["domain"] = apiOrigin,
                    ["prefersBorder"] = true
                },
                // ChatGPT's Apps SDK reads the same CSP/domain info from its own (snake_case)
                // metadata namespace, provided alongside _meta.ui so the widget is recognized in
                // ChatGPT as well as in SEP-1865 MCP Apps hosts.
                ["openai/widgetCSP"] = new JsonObject
                {
                    ["connect_domains"] = new JsonArray(),
                    ["resource_domains"] = new JsonArray()
                },
                ["openai/widgetDomain"] = apiOrigin,
                ["openai/widgetDescription"] =
                    "A read-only day view of the household meal plan: every planned meal per slot with its " +
                    "assigned people, portions, guests, and servings. Includes previous/next-day navigation.",
                ["openai/widgetPrefersBorder"] = true
            }
        });

    [Description("Shows the household meal plan for one day as a read-only interactive view in MCP hosts that " +
                 "support MCP Apps. Hosts without MCP Apps support receive a plain text summary instead. The view " +
                 "cannot change the plan — use the meal-planner tools (add_meal_to_plan, move_meal_in_plan, etc.) " +
                 "for edits, and get_meal_plan when you only need the raw data.")]
    private static async Task<CallToolResult> ShowMealPlan(
        MealPlannerService service,
        ICurrentUser currentUser,
        [Description("Day to show in yyyy-MM-dd format. Defaults to today.")] string? date = null,
        CancellationToken cancellationToken = default)
    {
        var parsedDate = date is null
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.TryParseExact(date, "yyyy-MM-dd", out var value)
                ? value
                : throw new McpException("Date must use yyyy-MM-dd format.");

        var plan = await service.GetForDateAsync(
            RequireUser(currentUser), RequireHouse(currentUser), parsedDate, cancellationToken);
        var data = ToUiData(plan);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = FallbackText(data) }],
            StructuredContent = JsonSerializer.SerializeToElement(data, JsonSerializerOptions.Web)
        };
    }

    private static MealPlanUiData ToUiData(DailyMealPlanResponse plan)
    {
        var slots = Slots
            .Select(slot => new MealPlanUiSlot(
                slot.Slot,
                slot.Label,
                (plan.Meals.TryGetValue(slot.Slot, out var items) ? items : [])
                    .OrderBy(item => item.SortOrder)
                    .Select(item => new MealPlanUiItem(
                        item.Id, item.Type, item.DisplayName, item.Note, item.Servings, item.Guests,
                        item.IngredientQuantity, item.IngredientUnit,
                        item.Participants
                            .Select(p => new MealPlanUiParticipant(p.DisplayName, p.IsCurrentUser, p.PortionPercent))
                            .ToList()))
                    .ToList()))
            .ToList();
        return new MealPlanUiData(plan.Date, slots, slots.Sum(slot => slot.Items.Count));
    }

    public static string ApiOrigin(OAuthOptions oauth) =>
        new Uri(oauth.Resource).GetLeftPart(UriPartial.Authority);

    private static string FallbackText(MealPlanUiData data)
    {
        if (data.MealCount == 0)
            return $"No meals planned for {data.Date}.";
        var lines = data.Slots
            .Where(slot => slot.Items.Count > 0)
            .SelectMany(slot => slot.Items.Select(item =>
            {
                var people = item.Participants.Count + item.Guests;
                var who = people == 0
                    ? "no one assigned"
                    : $"{people} {(people == 1 ? "person" : "people")}";
                return $"  • [{slot.Label}] {item.DisplayName} — {who}, {item.Servings:0.##} serving(s)"
                     + (item.Note is null ? "" : $" ({item.Note})");
            }));
        return $"Meal plan for {data.Date} ({data.MealCount} meal(s)):\n" + string.Join('\n', lines);
    }
}

/// <summary>Read-only meal-plan data for one day, consumed by the embedded MCP App UI.</summary>
public sealed record MealPlanUiData(
    string Date,
    IReadOnlyList<MealPlanUiSlot> Slots,
    int MealCount);

/// <summary>One meal slot (e.g. breakfast) and the meals planned in it.</summary>
public sealed record MealPlanUiSlot(
    string Slot,
    string Label,
    IReadOnlyList<MealPlanUiItem> Items);

/// <summary>One planned meal shown read-only in the embedded UI.</summary>
public sealed record MealPlanUiItem(
    Guid Id,
    string Type,
    string DisplayName,
    string? Note,
    decimal Servings,
    int Guests,
    decimal? IngredientQuantity,
    string? IngredientUnit,
    IReadOnlyList<MealPlanUiParticipant> Participants);

/// <summary>One household member assigned to a planned meal, with their portion.</summary>
public sealed record MealPlanUiParticipant(
    string DisplayName,
    bool IsCurrentUser,
    int PortionPercent);
