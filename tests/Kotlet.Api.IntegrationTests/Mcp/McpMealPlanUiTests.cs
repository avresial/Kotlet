using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

/// <summary>
/// Exercises the MCP Apps (SEP-1865) read-only meal-plan UI: the show_meal_plan tool advertises the
/// ui://kotlet/meal-plan-v1 resource, serves structured day data with a plain text fallback, and the
/// resource itself is served as a self-contained MCP App HTML document that only displays the plan.
/// </summary>
public sealed class McpMealPlanUiTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ToolsList_AdvertisesShowMealPlanWithUiResourceMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "tools/list", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"show_meal_plan\"", body);
        var showMealPlan = body[body.IndexOf("\"show_meal_plan\"", StringComparison.Ordinal)..];
        Assert.Contains("ui://kotlet/meal-plan-v1", showMealPlan);
        Assert.Contains("resourceUri", showMealPlan);
        // ChatGPT's Apps SDK links the tool to its widget via this key.
        Assert.Contains("openai/outputTemplate", showMealPlan);
        Assert.Contains("outputSchema", showMealPlan);
        Assert.Contains("\"slots\"", showMealPlan);
        Assert.Contains("Loading meal plan...", showMealPlan);
        Assert.Contains("Meal plan ready", showMealPlan);
    }

    [Fact]
    public async Task ResourcesList_ExposesMealPlanUiWithCspMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/list", new { });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ui://kotlet/meal-plan-v1", body);
        Assert.Contains("text/html;profile=mcp-app", body);
        Assert.Contains("openai/widgetCSP", body);
        Assert.Contains("openai/widgetDomain", body);
        Assert.Contains("openai/widgetDescription", body);

        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data: ", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine["data: ".Length..]);
        var resource = document.RootElement.GetProperty("result").GetProperty("resources")
            .EnumerateArray()
            .Single(item => item.GetProperty("uri").GetString() == "ui://kotlet/meal-plan-v1");
        var ui = resource.GetProperty("_meta").GetProperty("ui");
        Assert.Equal("http://localhost", ui.GetProperty("domain").GetString());
        // A read-only, text-only view: every CSP domain list is empty.
        Assert.Empty(ui.GetProperty("csp").GetProperty("resourceDomains").EnumerateArray());
        Assert.Empty(ui.GetProperty("csp").GetProperty("connectDomains").EnumerateArray());
        Assert.Empty(ui.GetProperty("csp").GetProperty("frameDomains").EnumerateArray());
    }

    [Fact]
    public async Task MealPlanUiResource_IsServedAsReadOnlySelfContainedMcpAppHtml()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/read", new { uri = "ui://kotlet/meal-plan-v1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", body);
        // The MCP Apps bridge handshake and read-only day view ship inline.
        Assert.Contains("ui/initialize", body);
        Assert.Contains("appInfo", body);
        Assert.DoesNotContain("clientInfo", body);
        Assert.Contains("protocolVersion", body);
        Assert.Contains("slot-section", body);
        Assert.Contains("Read-only", body);
        // Day navigation re-calls the same read-only tool.
        Assert.Contains("show_meal_plan", body);
        // The UI must stay self-contained: no external scripts, styles, or REST calls.
        Assert.DoesNotContain("src=\\\"http", body);
        Assert.DoesNotContain("fetch(", body);
    }

    [Fact]
    public async Task ShowMealPlan_ReturnsStructuredDayAndTextFallback()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredient = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = $"Oats {Guid.NewGuid():N}", measurementUnit = "g", caloriesPer100BaseUnits = 379 }
        });
        var ingredientId = ExtractGuidAfter(await ingredient.Content.ReadAsStringAsync(), "\"id\":\"");
        var title = $"Porridge {Guid.NewGuid():N}";
        var recipe = await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title,
                servings = 2,
                mealType = "breakfast",
                descriptionMarkdown = "1. Cook the oats.",
                ingredients = new[] { new { ingredientId, quantity = 80, unit = "g" } }
            }
        });
        var recipeId = ExtractGuidAfter(await recipe.Content.ReadAsStringAsync(), "\"id\":\"");
        var date = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        await CallTool(client, accessToken, "add_meal_to_plan", new
        {
            request = new { date, slot = "breakfast", recipeId }
        });

        var response = await CallTool(client, accessToken, "show_meal_plan", new { date });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(title, body);
        // Structured content feeds the embedded UI…
        Assert.Contains("structuredContent", body);
        Assert.Contains("\"slots\"", body);
        Assert.Contains("\"mealCount\":1", body);
        // …while hosts without MCP Apps support still get a readable text summary.
        Assert.Contains("Meal plan for", body);
        Assert.Contains("[Breakfast]", body);
    }

    private Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
        => McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-meal-ui");

    private static Guid ExtractGuidAfter(string body, string marker)
        => McpTestHelpers.ExtractGuidAfter(body, marker);

    private static Task<HttpResponseMessage> CallTool(
        HttpClient client, string accessToken, string name, object arguments, string protocolVersion = McpTestHelpers.DefaultProtocolVersion)
        => McpTestHelpers.CallTool(client, accessToken, name, arguments, protocolVersion);

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters, string protocolVersion = McpTestHelpers.DefaultProtocolVersion)
        => McpTestHelpers.SendMcp(client, accessToken, method, parameters, protocolVersion);
}
