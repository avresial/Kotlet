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

    /// <summary>Registers a user with a home and runs the OAuth PKCE flow for an MCP-scoped token.</summary>
    private async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"mcp-meal-ui-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var registrationToken = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var house = await client.PostAsJsonAsync("/api/houses", new { name = "MCP meal UI home" });
        house.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await house.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetProperty("accessToken").GetString());

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp"
        });
        var authorizeResponse = await client.GetAsync(authorization);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"]);
        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));
        tokenResponse.EnsureSuccessStatusCode();
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
        return (client, accessToken);
    }

    private static Guid ExtractGuidAfter(string body, string marker)
    {
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{marker}' not found in: {body}");
        start += marker.Length;
        return Guid.Parse(body.Substring(start, 36));
    }

    private static Task<HttpResponseMessage> CallTool(HttpClient client, string accessToken, string name, object arguments)
        => SendMcp(client, accessToken, "tools/call", new { name, arguments });

    private static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        request.Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters });
        return client.SendAsync(request);
    }
}
