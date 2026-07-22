using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Auth;

/// <summary>
/// Exercises the RFC 7591 Dynamic Client Registration path that Anthropic's clients
/// (Claude Code / Desktop / claude.ai) rely on: discover the metadata, register a
/// client carrying their own loopback redirect URI, then complete the PKCE flow.
/// </summary>
public sealed class DynamicClientRegistrationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Theory]
    [InlineData("/.well-known/openid-configuration")]
    [InlineData("/.well-known/oauth-authorization-server")]
    public async Task Metadata_AdvertisesRegistrationEndpoint(string path)
    {
        var client = factory.CreateClient();
        var metadata = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(
            "http://localhost/connect/register",
            metadata.GetProperty("registration_endpoint").GetString());
    }

    [Fact]
    public async Task RegisteredClient_CompletesAuthorizationCodeFlow()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // A signed-in session is required so /connect/authorize can issue a code instead of
        // redirecting to the login page; registration itself is anonymous.
        var email = $"dcr-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var accessToken = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var houseResponse = await client.PostAsJsonAsync("/api/houses", new { name = "DCR home" });
        houseResponse.EnsureSuccessStatusCode();

        const string redirectUri = "http://localhost:51837/callback";
        var registerResponse = await client.PostAsJsonAsync("/connect/register", new
        {
            redirect_uris = new[] { redirectUri },
            client_name = "Claude test client",
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" }
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registered = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientId = registered.GetProperty("client_id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(clientId));
        Assert.Equal("none", registered.GetProperty("token_endpoint_auth_method").GetString());

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = "mcp offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp",
            ["state"] = "dcr-state"
        });

        var authorizeResponse = await client.GetAsync(authorization);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        Assert.StartsWith(redirectUri, authorizeResponse.Headers.Location!.AbsoluteUri);
        var callback = QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query);
        Assert.Equal("dcr-state", callback["state"]);
        var code = Assert.Single(callback["code"]);

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId!,
                ["code"] = code!,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = verifier,
                ["resource"] = "http://localhost/mcp"
            }));
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("access_token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(token.GetProperty("refresh_token").GetString()));
    }

    [Fact]
    public async Task Register_RejectsDisallowedRedirectUri()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/connect/register", new
        {
            redirect_uris = new[] { "http://evil.example.com/callback" }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_redirect_uri",
            (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
    }
}
