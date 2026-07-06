using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Shopping;

public sealed class ShoppingListEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/shopping-list")).StatusCode);
    }

    [Fact]
    public async Task Item_CanBeAddedPricedUpdatedListedAndRemoved()
    {
        var client = await CreateAuthenticatedClient();
        var ingredientId = await CreateIngredient(client, 7.25m);

        var createResponse = await client.PostAsJsonAsync("/api/shopping-list", new { ingredientId, quantity = 200m });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(14.50m, created.GetProperty("totalPrice").GetDecimal());

        var duplicate = await client.PostAsJsonAsync("/api/shopping-list", new { ingredientId, quantity = 1m });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var updateResponse = await client.PutAsJsonAsync($"/api/shopping-list/{id}", new { quantity = 300m, isPurchased = true });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(21.75m, updated.GetProperty("totalPrice").GetDecimal());
        Assert.True(updated.GetProperty("isPurchased").GetBoolean());

        var items = await client.GetFromJsonAsync<JsonElement[]>("/api/shopping-list");
        Assert.Contains(items!, item => item.GetProperty("id").GetGuid() == id);

        var clearResponse = await client.DeleteAsync("/api/shopping-list/checked");
        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var clearResult = await clearResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, clearResult.GetProperty("removed").GetInt32());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/shopping-list"))!);
    }

    [Fact]
    public async Task ShoppingList_ReturnsTranslatedIngredientNameAndMeasurementUnit()
    {
        var client = await CreateAuthenticatedClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("pl");
        var translatedName = $"Zakupy {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Shopping {Guid.NewGuid():N}",
            translation = translatedName,
            measurementUnit = "ml",
            isCountable = false,
            measurementUnitsPerPiece = (decimal?)null,
            caloriesPer100BaseUnits = 50m,
            pricePer100BaseUnits = 2m
        });
        var ingredient = await response.Content.ReadFromJsonAsync<JsonElement>();

        var createdResponse = await client.PostAsJsonAsync("/api/shopping-list", new
        {
            ingredientId = ingredient.GetProperty("id").GetGuid(),
            quantity = 100m
        });
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(translatedName, created.GetProperty("ingredientName").GetString());
        Assert.Equal("ml", created.GetProperty("measurementUnit").GetString());
    }

    private static async Task<Guid> CreateIngredient(HttpClient client, decimal price)
    {
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"Shopping ingredient {Guid.NewGuid():N}",
            measurementUnit = "g",
            caloriesPer100BaseUnits = 100m,
            pricePer100BaseUnits = price
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"shopping-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
