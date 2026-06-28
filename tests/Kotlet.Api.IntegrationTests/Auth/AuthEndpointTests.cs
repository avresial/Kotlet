using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Kotlet.Domain.Houses;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        var userId = body.GetProperty("user").GetProperty("id").GetGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KotletDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == userId);
            var house = await db.Houses.AsNoTracking().SingleAsync(item => item.Id == user.HouseId);
            Assert.Equal(DefaultHouse.Id, user.HouseId);
            Assert.Equal(DefaultHouse.Name, house.Name);
        }
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("kotlet_refresh=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_DefaultsDisplayNameToEmailPrefix()
    {
        var client = _factory.CreateClient();
        var email = $"default-name-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email.Split('@')[0], body.GetProperty("user").GetProperty("displayName").GetString());
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

    [Fact]
    public async Task UpdateProfile_ChangesDisplayName()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var response = await client.PutAsJsonAsync("/api/auth/profile", new { displayName = "Head Chef" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Head Chef", body.GetProperty("displayName").GetString());

        var me = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
        Assert.Equal("Head Chef", me.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task UpdateProfile_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/auth/profile", new { displayName = "Anonymous" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_UpdatesPasswordAndAllowsLoginWithNewPassword()
    {
        var client = _factory.CreateClient();
        var email = await Authenticate(client);

        var change = await client.PostAsJsonAsync("/api/auth/password", new
        {
            currentPassword = "Password1!",
            newPassword = "NewPassword2!",
            confirmPassword = "NewPassword2!"
        });
        Assert.Equal(HttpStatusCode.NoContent, change.StatusCode);

        var loginClient = _factory.CreateClient();
        var oldLogin = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);
        var newLogin = await loginClient.PostAsJsonAsync("/api/auth/login", new { email, password = "NewPassword2!" });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_RejectsIncorrectCurrentPassword()
    {
        var client = _factory.CreateClient();
        await Authenticate(client);

        var change = await client.PostAsJsonAsync("/api/auth/password", new
        {
            currentPassword = "WrongPassword!",
            newPassword = "NewPassword2!",
            confirmPassword = "NewPassword2!"
        });

        Assert.Equal(HttpStatusCode.BadRequest, change.StatusCode);
    }

    [Fact]
    public async Task House_RequiresAuthentication()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/house")).StatusCode);
    }

    [Fact]
    public async Task House_ReturnsMembersOfCurrentUsersHouseWithCurrentUserFirst()
    {
        var client = _factory.CreateClient();
        var email = await Authenticate(client);

        var house = await client.GetFromJsonAsync<JsonElement>("/api/auth/house");

        Assert.Equal(DefaultHouse.Id, house.GetProperty("id").GetGuid());
        Assert.Equal(DefaultHouse.Name, house.GetProperty("name").GetString());
        var members = house.GetProperty("members").EnumerateArray().ToList();
        Assert.NotEmpty(members);
        var current = Assert.Single(members, member => member.GetProperty("email").GetString() == email);
        Assert.True(current.GetProperty("isCurrentUser").GetBoolean());
        Assert.True(members[0].GetProperty("isCurrentUser").GetBoolean());
        Assert.Contains(members, member => member.GetProperty("email").GetString() == email);
    }

    private static async Task<string> Authenticate(HttpClient client)
    {
        var email = $"cook-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return email;
    }

    private static Task<HttpResponseMessage> Register(HttpClient client) => client.PostAsJsonAsync("/api/auth/register", new
    {
        email = $"cook-{Guid.NewGuid():N}@example.com",
        password = "Password1!",
        confirmPassword = "Password1!"
    });
}
