using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Kotlet.Domain.Auth;
using Kotlet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kotlet.Api.IntegrationTests.Admin;

public sealed class AdminEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Users_RequireAuthentication()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task Users_CanBeListedUpdatedAndDeleted_WithoutExposingPassword()
    {
        var admin = factory.CreateClient();
        await Authenticate(admin, true);
        var managed = factory.CreateClient();
        var managedUser = await Authenticate(managed);

        Assert.Equal(HttpStatusCode.Forbidden, (await managed.GetAsync("/api/admin/users")).StatusCode);

        var listResponse = await admin.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var json = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(managedUser.GetProperty("email").GetString()!, json);

        var id = managedUser.GetProperty("id").GetGuid();
        var update = await admin.PutAsJsonAsync($"/api/admin/users/{id}", new
        {
            email = $"updated-{Guid.NewGuid():N}@example.com",
            displayName = "Updated Cook",
            preferredLanguage = "pl"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated Cook", updated.GetProperty("displayName").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/admin/users/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await managed.GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await managed.PostAsync("/api/auth/refresh", null)).StatusCode);
    }

    private async Task<JsonElement> Authenticate(HttpClient client, bool admin = false)
    {
        var email = $"admin-test-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([RoleNames.User], body.GetProperty("user").GetProperty("roles").EnumerateArray().Select(x => x.GetString()!).ToArray());
        if (admin)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KotletDbContext>();
            var user = await db.Users.Include(x => x.Roles).SingleAsync(x => x.Email == email);
            user.Roles.Add(await db.Roles.SingleAsync(x => x.Name == RoleNames.Admin));
            await db.SaveChangesAsync();
            response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Password1!" });
            body = await response.Content.ReadFromJsonAsync<JsonElement>();
        }
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("user");
    }
}
