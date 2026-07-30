using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kotlet.Api.Auth;
using Kotlet.Api.Mcp;
using Kotlet.Application.MealPlanner;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Kotlet.Api.Mcp.McpHelpers;

namespace Kotlet.Api.MealPlanner;

/// <summary>
/// Read-only weekly draft preview for MCP Apps hosts. The preview uses exactly the same
/// request shape as <c>add_weekly_meal_plan</c>, so approval never requires the agent to
/// translate or rebuild its plan. The embedded app can submit that request after an
/// explicit user click; non-UI hosts receive a concise text preview.
/// </summary>
public static class MealPlanUiMcp
{
    public const string ToolName = "preview_meal_plan";
    public const string ResourceUri = "ui://kotlet/meal-plan-preview-v1";
    public const string ResourceMimeType = "text/html;profile=mcp-app";

    private static readonly Lazy<string> AppHtml = new(() =>
    {
        var assembly = typeof(MealPlanUiMcp).Assembly;
        const string name = "Kotlet.Api.MealPlanner.MealPlanUiApp.html";
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static McpServerTool CreatePreviewTool(IServiceProvider services) =>
        McpServerTool.Create(PreviewMealPlan, new McpServerToolCreateOptions
        {
            Services = services,
            Name = ToolName,
            Title = "Preview weekly meal plan",
            ReadOnly = true,
            Destructive = false,
            Idempotent = true,
            OpenWorld = false,
            UseStructuredContent = true,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject { ["resourceUri"] = ResourceUri },
                ["openai/outputTemplate"] = ResourceUri,
                ["openai/toolInvocation/invoking"] = "Building meal plan preview...",
                ["openai/toolInvocation/invoked"] = "Draft meal plan ready"
            }
        });

    public static McpServerResource CreateUiResource(string apiOrigin) =>
        McpServerResource.Create(() => AppHtml.Value, new McpServerResourceCreateOptions
        {
            UriTemplate = ResourceUri,
            Name = "meal-plan-preview-ui",
            Title = "Kotlet meal plan preview",
            Description = "Interactive weekly draft with an explicit Add to Kotlet action.",
            MimeType = ResourceMimeType,
            Meta = new JsonObject
            {
                ["ui"] = new JsonObject
                {
                    ["csp"] = new JsonObject
                    {
                        ["connectDomains"] = new JsonArray(),
                        ["resourceDomains"] = new JsonArray(),
                        ["frameDomains"] = new JsonArray()
                    },
                    ["domain"] = apiOrigin,
                    ["prefersBorder"] = true
                },
                ["openai/widgetCSP"] = new JsonObject
                {
                    ["connect_domains"] = new JsonArray(),
                    ["resource_domains"] = new JsonArray()
                },
                ["openai/widgetDomain"] = apiOrigin,
                ["openai/widgetDescription"] =
                    "A seven-day Kotlet meal plan draft grouped by day, with recipe ingredients and a confirmation button.",
                ["openai/widgetPrefersBorder"] = true
            }
        });

    [Description("Validates and previews a draft meal plan without saving anything. Use this after compact ingredient, recipe, and prepared-meal discovery. The request is identical to add_weekly_meal_plan, so after the user approves, submit it unchanged. Compatible hosts render a Kotlet weekly planner with an Add to Kotlet button.")]
    private static async Task<CallToolResult> PreviewMealPlan(
        [Description("Complete seven-day draft. Each meal must set exactly one of recipeId, ingredientId, or preparedMealId. Dates must fall between weekStart and weekStart + 6 days.")]
        AddWeeklyMealPlanRequest request,
        MealPlannerService service,
        ICurrentUser currentUser,
        CancellationToken cancellationToken = default)
    {
        var result = await service.PreviewWeekAsync(
            RequireHouse(currentUser), request, cancellationToken);
        if (result.Status != MealPlannerOperationStatus.Success || result.Preview is null)
            throw new McpException(ValidationMessage(result.ValidationErrors));

        var data = new MealPlanPreviewUiData(result.Preview, request);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = FallbackText(result.Preview) }],
            StructuredContent = JsonSerializer.SerializeToElement(data, JsonSerializerOptions.Web)
        };
    }

    private static string ValidationMessage(IReadOnlyDictionary<string, string[]>? errors) =>
        errors is not { Count: > 0 }
            ? "The meal plan draft is invalid."
            : "The meal plan draft is invalid: " + string.Join(
                "; ", errors.Select(error => $"{error.Key}: {string.Join(", ", error.Value)}"));

    private static string FallbackText(MealPlanPreviewResponse preview)
    {
        var lines = preview.Meals.Select(meal =>
            $"- {meal.Date} · {SlotLabel(meal.Slot)}: {meal.Title}"
            + (meal.Ingredients.Count == 0
                ? ""
                : $" ({string.Join(", ", meal.Ingredients.Take(4))}"
                  + (meal.Ingredients.Count > 4 ? ", …" : "")
                  + ")"));
        return $"Draft meal plan for {preview.WeekStart} ({preview.Meals.Count} meals; not saved):\n"
               + string.Join('\n', lines)
               + "\n\nAsk the user to approve it, then call add_weekly_meal_plan with the same request.";
    }

    private static string SlotLabel(string slot) => slot switch
    {
        "second-breakfast" => "Second breakfast",
        _ => char.ToUpperInvariant(slot[0]) + slot[1..]
    };

}

public sealed record MealPlanPreviewUiData(
    MealPlanPreviewResponse Preview,
    AddWeeklyMealPlanRequest SaveRequest);
