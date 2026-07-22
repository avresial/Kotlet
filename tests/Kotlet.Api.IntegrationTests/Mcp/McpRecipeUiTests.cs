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
/// Exercises the MCP Apps (SEP-1865) recipe UI proof of concept: the show_recipes tool
/// advertises the ui://kotlet/recipes-v2 resource, serves structured card data with a plain
/// text fallback, and the resource itself is served as an MCP App HTML document.
/// </summary>
public sealed class McpRecipeUiTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ToolsList_AdvertisesShowRecipesWithUiResourceMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "tools/list", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"show_recipes\"", body);
        var showRecipes = body[body.IndexOf("\"show_recipes\"", StringComparison.Ordinal)..];
        Assert.Contains("ui://kotlet/recipes-v2", showRecipes);
        Assert.Contains("resourceUri", showRecipes);
        // ChatGPT's Apps SDK links the tool to its widget via this key.
        Assert.Contains("openai/outputTemplate", showRecipes);
    }

    [Fact]
    public async Task ResourcesList_ExposesRecipeUiWithCspMetadata()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/list", new { });

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("ui://kotlet/recipes-v2", body);
        Assert.Contains("text/html;profile=mcp-app", body);
        // Hosts enforce the iframe CSP from the full connect/resource/frame domain shape.
        Assert.Contains("connectDomains", body);
        Assert.Contains("resourceDomains", body);
        Assert.Contains("frameDomains", body);
        Assert.Contains("\"domain\":", body);
        // ChatGPT's Apps SDK reads its own snake_case CSP/domain namespace.
        Assert.Contains("openai/widgetCSP", body);
        Assert.Contains("resource_domains", body);
        Assert.Contains("openai/widgetDomain", body);
    }

    [Fact]
    public async Task RecipeUiResource_IsServedAsSelfContainedMcpAppHtml()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();

        var response = await SendMcp(client, accessToken, "resources/read", new { uri = "ui://kotlet/recipes-v2" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", body);
        // Card grid, the MCP Apps bridge handshake, and the detail-view tool call all ship inline.
        Assert.Contains("recipe-grid", body);
        Assert.Contains("ui/initialize", body);
        // Hosts reject the ui/initialize handshake unless it carries a protocolVersion string.
        Assert.Contains("protocolVersion", body);
        Assert.Contains("get_recipe", body);
        // The UI must stay self-contained: no external scripts, styles, or REST calls.
        Assert.DoesNotContain("src=\\\"http", body);
        Assert.DoesNotContain("fetch(", body);
    }

    [Fact]
    public async Task ShowRecipes_ReturnsStructuredCardsAndTextFallback()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredient = await CallTool(client, accessToken, "create_ingredient", new
        {
            request = new { name = $"Chickpeas {Guid.NewGuid():N}", measurementUnit = "g", caloriesPer100BaseUnits = 164 }
        });
        var ingredientId = ExtractGuidAfter(await ingredient.Content.ReadAsStringAsync(), "\"id\":\"");
        var title = $"Chickpea stew {Guid.NewGuid():N}";
        await CallTool(client, accessToken, "add_recipe", new
        {
            request = new
            {
                title,
                servings = 3,
                mealType = "dinner",
                descriptionMarkdown = "1. Simmer the chickpeas.",
                ingredients = new[] { new { ingredientId, quantity = 240, unit = "g" } }
            }
        });

        var response = await CallTool(client, accessToken, "show_recipes", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains(title, body);
        // Structured content feeds the embedded UI…
        Assert.Contains("structuredContent", body);
        Assert.Contains("\"apiOrigin\"", body);
        Assert.Contains("\"ingredientCount\":1", body);
        // …while hosts without MCP Apps support still get a readable text list.
        Assert.Contains("Household recipes", body);
        Assert.Contains("3 serving(s)", body);
    }

    /// <summary>Registers a user with a home and runs the OAuth PKCE flow for an MCP-scoped token.</summary>
    private async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"mcp-ui-{Guid.NewGuid():N}@example.com";
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
        var house = await client.PostAsJsonAsync("/api/houses", new { name = "MCP UI home" });
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
