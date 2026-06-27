using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Auth;

public sealed class AuthEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Register_IssuesAccessTokenAndRefreshCookie()
    {
        var client = _factory.CreateClient();
        var response = await Register(client);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("accessToken").GetString()));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("kotlet_refresh=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Auth_AllowsCrossOriginCredentialedRequests()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", "https://frontend.example");
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal("https://frontend.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    [Fact]
    public async Task Me_RequiresValidBearerToken()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);

        var registration = await Register(client);
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Refresh_RotatesCookieAndOldTokenCannotBeReused()
    {
        var client = _factory.CreateClient();
        var registration = await Register(client);
        var oldCookie = Assert.Single(registration.Headers.GetValues("Set-Cookie")).Split(';')[0];

        var refresh = await client.PostAsync("/api/auth/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var newCookie = Assert.Single(refresh.Headers.GetValues("Set-Cookie")).Split(';')[0];
        Assert.NotEqual(oldCookie, newCookie);

        using var reuse = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        reuse.Headers.Add("Cookie", oldCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient().SendAsync(reuse)).StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesRefreshTokenAndClearsCookie()
    {
        var client = _factory.CreateClient();
        await Register(client);
        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("expires=Thu, 01 Jan 1970", Assert.Single(logout.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    private static Task<HttpResponseMessage> Register(HttpClient client) => client.PostAsJsonAsync("/api/auth/register", new
    {
        email = $"cook-{Guid.NewGuid():N}@example.com",
        password = "Password1!",
        confirmPassword = "Password1!"
    });
}
