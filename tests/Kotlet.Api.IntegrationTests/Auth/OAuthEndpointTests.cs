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
        await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });

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
        Assert.Contains("http://localhost/mcp", new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Audiences);
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("refresh_token").GetString()));

        using var toolRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        toolRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        toolRequest.Headers.Accept.ParseAdd("application/json");
        toolRequest.Headers.Accept.ParseAdd("text/event-stream");
        toolRequest.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        toolRequest.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new { name = "who_am_i", arguments = new { } }
        });
        var toolResponse = await client.SendAsync(toolRequest);
        Assert.Equal(HttpStatusCode.OK, toolResponse.StatusCode);
        Assert.Contains(email, await toolResponse.Content.ReadAsStringAsync());

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
}
