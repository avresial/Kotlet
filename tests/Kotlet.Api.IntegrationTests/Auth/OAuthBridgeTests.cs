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
/// Covers the OAuth session bridge that lets a mobile client finish authorization even though the
/// refresh cookie set by the cross-site login fetch is blocked as a third-party cookie. The bridge
/// runs first-party to the API origin, so the cookie it sets is honoured; these tests exercise it
/// on a fresh client that carries no login cookie of its own.
/// </summary>
public sealed class OAuthBridgeTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Bridge_EstablishesSession_ThatAuthorizeEndpointConsumes()
    {
        var accessToken = await RegisterUserAsync();

        // A brand-new client with an empty cookie jar stands in for the mobile browser that never
        // stored the third-party login cookie.
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var returnUrl = AuthorizeUrl(out var verifier);

        var bridge = await browser.PostAsync("/api/auth/oauth-bridge", Form(accessToken, returnUrl));
        Assert.Equal(HttpStatusCode.Redirect, bridge.StatusCode);
        Assert.Equal("/connect/authorize", bridge.Headers.Location!.AbsolutePath);
        Assert.Contains(bridge.Headers, header => header.Key == "Set-Cookie");

        var authorize = await browser.GetAsync(returnUrl);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var callback = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query);
        Assert.Equal("bridge-state", callback["state"]);
        var code = Assert.Single(callback["code"]);

        var token = await browser.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));
        Assert.Equal(HttpStatusCode.OK, token.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(
            (await token.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("access_token").GetString()));
    }

    [Fact]
    public async Task Bridge_RejectsInvalidAccessToken()
    {
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await browser.PostAsync("/api/auth/oauth-bridge", Form("not-a-valid-token", AuthorizeUrl(out _)));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Bridge_RejectsForeignReturnUrl()
    {
        var accessToken = await RegisterUserAsync();
        var browser = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await browser.PostAsync("/api/auth/oauth-bridge",
            Form(accessToken, "https://evil.example.com/connect/authorize"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> RegisterUserAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"bridge-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        return (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
    }

    private static FormUrlEncodedContent Form(string token, string returnUrl) => new(new Dictionary<string, string>
    {
        ["token"] = token,
        ["returnUrl"] = returnUrl
    });

    private static string AuthorizeUrl(out string verifier)
    {
        verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return "http://localhost" + QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp offline_access",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp",
            ["state"] = "bridge-state"
        });
    }
}
