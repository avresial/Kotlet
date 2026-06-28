using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

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
        await Authenticate(admin);
        var managed = factory.CreateClient();
        var managedUser = await Authenticate(managed);

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

    private static async Task<JsonElement> Authenticate(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"admin-test-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        return body.GetProperty("user");
    }
}
