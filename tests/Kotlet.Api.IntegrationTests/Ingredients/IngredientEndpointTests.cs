using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Ingredients;

public sealed class IngredientEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ingredients")).StatusCode);
    }

    [Fact]
    public async Task Ingredient_CanBeCreatedListedUpdatedAndDeleted()
    {
        var client = await CreateAuthenticatedClient();
        var name = $"Ingredient {Guid.NewGuid():N}";

        var create = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name, measurementUnit = "g", isCountable = false, measurementUnitsPerPiece = (decimal?)null,
            caloriesPer100BaseUnits = 165.5m, pricePer100BaseUnits = 12.99m
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/ingredients");
        Assert.Contains(list!, item => item.GetProperty("id").GetGuid() == id);

        var update = await client.PutAsJsonAsync($"/api/ingredients/{id}", new
        {
            name = $"{name} updated", measurementUnit = "ml", isCountable = true, measurementUnitsPerPiece = 250m,
            caloriesPer100BaseUnits = 170m, pricePer100BaseUnits = 15m
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ml", updated.GetProperty("measurementUnit").GetString());
        Assert.True(updated.GetProperty("isCountable").GetBoolean());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/ingredients/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/ingredients/{id}")).StatusCode);
    }

    [Fact]
    public async Task Create_RejectsInvalidValues()
    {
        var client = await CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = "", measurementUnit = "bucket", isCountable = true, measurementUnitsPerPiece = (decimal?)null,
            caloriesPer100BaseUnits = -1, pricePer100BaseUnits = -1
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"ingredient-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
