using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Pantry;

public sealed class PantryRecipeMatchEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task RecipeMatches_RequireAuthentication()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/pantry/recipe-matches")).StatusCode);
    }

    [Fact]
    public async Task RecipeMatches_ReflectPantryChanges()
    {
        var client = await CreateAuthenticatedClient("pantry-matches");
        var (flourId, _) = await CreateIngredient(client, "Match flour");
        var (milkId, milkName) = await CreateIngredient(client, "Match milk");
        var recipeResponse = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = $"Pancakes {Guid.NewGuid():N}",
            descriptionMarkdown = (string?)null,
            servings = 2,
            ingredients = new[]
            {
                new { ingredientId = flourId, quantity = 200m, unit = "g", note = (string?)null },
                new { ingredientId = milkId, quantity = 300m, unit = "g", note = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, recipeResponse.StatusCode);
        var recipeId = (await recipeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        // Empty pantry: no suggestions (and the empty result gets cached).
        Assert.Empty((await client.GetFromJsonAsync<JsonElement[]>("/api/pantry/recipe-matches"))!);

        // Adding a pantry item invalidates the cache; the recipe now matches partially.
        await client.PostAsJsonAsync("/api/pantry", new { ingredientId = flourId, quantity = 500m });
        var partial = Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/pantry/recipe-matches"))!);
        Assert.Equal(recipeId, partial.GetProperty("recipeId").GetGuid());
        Assert.Equal(1, partial.GetProperty("matchedIngredientCount").GetInt32());
        Assert.Equal(2, partial.GetProperty("totalIngredientCount").GetInt32());
        Assert.False(partial.GetProperty("isFullMatch").GetBoolean());
        var missing = Assert.Single(partial.GetProperty("missingIngredients").EnumerateArray().ToArray());
        Assert.Equal(milkName, missing.GetProperty("name").GetString());

        // Completing the ingredients turns it into a full match.
        await client.PostAsJsonAsync("/api/pantry", new { ingredientId = milkId, quantity = 500m });
        var full = Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/pantry/recipe-matches"))!);
        Assert.True(full.GetProperty("isFullMatch").GetBoolean());
        Assert.Empty(full.GetProperty("missingIngredients").EnumerateArray());
    }

    private static async Task<(Guid Id, string Name)> CreateIngredient(HttpClient client, string prefix)
    {
        var name = $"{prefix} {Guid.NewGuid():N}";
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name,
            measurementUnit = "g",
            caloriesPer100BaseUnits = 100m,
            pricePer100BaseUnits = 5m
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), name);
    }

    private async Task<HttpClient> CreateAuthenticatedClient(string prefix)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{prefix}-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
