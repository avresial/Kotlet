using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Auth;

public sealed class OAuthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task AuthorizationCodeFlow_UsesPkceAndBindsTokenToMcpResource()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var unauthorizedMcp = await client.PostAsync("/mcp", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedMcp.StatusCode);
        Assert.Contains(unauthorizedMcp.Headers.WwwAuthenticate,
            header => header.Parameter?.Contains("resource_metadata", StringComparison.Ordinal) == true);

        var email = $"oauth-{Guid.NewGuid():N}@example.com";
        var registrationResponse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registrationResponse.EnsureSuccessStatusCode();
        var registrationToken = (await registrationResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var houseResponse = await client.PostAsJsonAsync("/api/houses", new { name = "OAuth home" });
        houseResponse.EnsureSuccessStatusCode();
        var houseId = (await houseResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("house").GetProperty("id").GetGuid();

        var metadata = await client.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration");
        Assert.EndsWith("/connect/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.EndsWith("/connect/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Contains("S256", metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(item => item.GetString()));

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp",
            ["state"] = "test-state"
        });

        var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var loginRedirect = await anonymous.GetAsync(authorization);
        Assert.StartsWith("http://localhost:4200/login?returnUrl=", loginRedirect.Headers.Location!.AbsoluteUri);

        var authorizeResponse = await client.GetAsync(authorization);
        Assert.True(authorizeResponse.StatusCode == HttpStatusCode.Redirect,
            await authorizeResponse.Content.ReadAsStringAsync());
        var callback = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query);
        Assert.Equal("test-state", callback["state"]);
        var code = Assert.Single(callback["code"]);

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));

        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = token.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Contains("http://localhost/mcp", jwt.Audiences);
        Assert.Equal(houseId.ToString(), jwt.Claims.Single(claim => claim.Type == "house_id").Value);
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("refresh_token").GetString()));

        var toolResponse = await CallTool(client, accessToken!, "who_am_i", new { });
        Assert.Equal(HttpStatusCode.OK, toolResponse.StatusCode);
        Assert.Contains(email, await toolResponse.Content.ReadAsStringAsync());

        var recipesResponse = await CallTool(client, accessToken!, "get_recipes", new { });
        Assert.Equal(HttpStatusCode.OK, recipesResponse.StatusCode);
        Assert.Contains("totalCount", await recipesResponse.Content.ReadAsStringAsync());

        var mealPlanResponse = await CallTool(client, accessToken!, "get_meal_plan", new { date = "2026-06-29" });
        Assert.Equal(HttpStatusCode.OK, mealPlanResponse.StatusCode);
        Assert.Contains("breakfast", await mealPlanResponse.Content.ReadAsStringAsync());

        var weeklyPlanResponse = await CallTool(client, accessToken!, "add_weekly_meal_plan",
            new { request = new { weekStart = "2026-06-29", meals = Array.Empty<object>() } });
        Assert.Equal(HttpStatusCode.OK, weeklyPlanResponse.StatusCode);
        Assert.Contains("added", await weeklyPlanResponse.Content.ReadAsStringAsync());

        var shoppingListResponse = await CallTool(client, accessToken!, "get_shopping_list", new { });
        Assert.Equal(HttpStatusCode.OK, shoppingListResponse.StatusCode);
        Assert.DoesNotContain("isError\":true", await shoppingListResponse.Content.ReadAsStringAsync());

        var invalidResource = await client.GetAsync(authorization.Replace(
            Uri.EscapeDataString("http://localhost/mcp"),
            Uri.EscapeDataString("http://localhost/not-mcp"),
            StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResource.StatusCode);
        Assert.Contains("invalid_target", await invalidResource.Content.ReadAsStringAsync());

        var invalidRedirect = await client.GetAsync(authorization.Replace(
            Uri.EscapeDataString("http://127.0.0.1/callback"),
            Uri.EscapeDataString("http://127.0.0.1/wrong"),
            StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRedirect.StatusCode);

        var secondAuthorization = await client.GetAsync(authorization);
        var secondCode = Assert.Single(QueryHelpers.ParseQuery(secondAuthorization.Headers.Location!.Query)["code"]);
        var invalidVerifier = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = secondCode!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier + "wrong",
            ["resource"] = "http://localhost/mcp"
        }));
        Assert.Equal(HttpStatusCode.BadRequest, invalidVerifier.StatusCode);
        Assert.Equal("invalid_grant", (await invalidVerifier.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }

    private static Task<HttpResponseMessage> CallTool(HttpClient client, string accessToken, string name, object arguments)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        request.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name, arguments }
        });
        return client.SendAsync(request);
    }
}
