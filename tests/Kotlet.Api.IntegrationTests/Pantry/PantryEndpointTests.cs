using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Pantry;

public sealed class PantryEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pantry")).StatusCode);
    }

    [Fact]
    public async Task Pantry_ItemCanBeAddedUpdatedListedAndRemoved()
    {
        var client = await CreateAuthenticatedClient("pantry");
        var ingredientId = await CreateIngredient(client);

        var createdResponse = await client.PostAsJsonAsync("/api/pantry", new { ingredientId, quantity = 2.5m });
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();
        Assert.Equal(2.5m, created.GetProperty("quantity").GetDecimal());

        var updatedResponse = await client.PutAsJsonAsync($"/api/pantry/{itemId}", new { quantity = 1.25m });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var items = await client.GetFromJsonAsync<JsonElement[]>("/api/pantry");
        Assert.Contains(items!, item => item.GetProperty("id").GetGuid() == itemId && item.GetProperty("quantity").GetDecimal() == 1.25m);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/pantry/{itemId}")).StatusCode);
    }

    [Fact]
    public async Task Pantry_IsSharedByUsersInTheSameHouse()
    {
        var (owner, other, _) = await TestAuth.HouseholdAsync(factory, "pantry-house");
        var ingredientId = await CreateIngredient(owner);
        var response = await owner.PostAsJsonAsync("/api/pantry", new { ingredientId, quantity = 3m });
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = created.GetProperty("id").GetGuid();

        var otherUsersItems = await other.GetFromJsonAsync<JsonElement[]>("/api/pantry");
        Assert.Contains(otherUsersItems!, item => item.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(HttpStatusCode.OK, (await other.PutAsJsonAsync($"/api/pantry/{itemId}", new { quantity = 9m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await other.DeleteAsync($"/api/pantry/{itemId}")).StatusCode);
    }

    private static async Task<Guid> CreateIngredient(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Pantry ingredient {Guid.NewGuid():N}", measurementUnit = "g",
            caloriesPer100BaseUnits = 100m, pricePer100BaseUnits = 5m
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> CreateAuthenticatedClient(string prefix)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():N}@example.com", password = "Password1!", confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
