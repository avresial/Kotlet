using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Recipes;

public sealed class RecipeEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory = factory;

    [Fact]
    public async Task Endpoints_RequireAuthentication()
    {
        var client = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/recipes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/recipes", new { })).StatusCode);
    }

    [Fact]
    public async Task Recipe_CanBeCreatedListedViewedUpdatedAndDeleted()
    {
        var client = await CreateAuthenticatedClient();
        var tomatoesId = await CreateIngredient(client, "Tomatoes", false, null);
        var garlicId = await CreateIngredient(client, "Garlic", true, 5m);

        // Create
        var create = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = "Tomato Soup",
            descriptionMarkdown = "Simple **soup**.",
            mealType = "dinner",
            ingredients = new[]
            {
                new { ingredientId = tomatoesId, quantity = 800, unit = "g", note = (string?)"canned" },
                new { ingredientId = garlicId, quantity = 2, unit = "piece", note = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Tomato Soup", created.GetProperty("title").GetString());
        Assert.Equal("tomato-soup", created.GetProperty("slug").GetString());
        Assert.Equal("dinner", created.GetProperty("mealType").GetString());
        Assert.Equal(2, created.GetProperty("ingredients").GetArrayLength());
        var garlic = created.GetProperty("ingredients")[1];
        Assert.Equal(10m, garlic.GetProperty("normalizedQuantity").GetDecimal());
        Assert.Equal("g", garlic.GetProperty("normalizedUnit").GetString());

        // List
        var list = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        Assert.True(list.GetProperty("totalCount").GetInt32() >= 1);
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, r => r.GetProperty("id").GetGuid() == id && r.GetProperty("mealType").GetString() == "dinner");
        var matchingType = await client.GetFromJsonAsync<JsonElement>("/api/recipes?mealType=dinner");
        Assert.Contains(matchingType.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == id);
        var otherType = await client.GetFromJsonAsync<JsonElement>("/api/recipes?mealType=breakfast");
        Assert.DoesNotContain(otherType.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/recipes?mealType=brunch")).StatusCode);

        // Get detail
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{id}");
        Assert.Equal(id, detail.GetProperty("id").GetGuid());
        Assert.Equal("Simple **soup**.", detail.GetProperty("descriptionMarkdown").GetString());
        Assert.Equal("dinner", detail.GetProperty("mealType").GetString());

        // Update
        var update = await client.PutAsJsonAsync($"/api/recipes/{id}", new
        {
            title = "Cream of Tomato",
            descriptionMarkdown = "Rich and creamy.",
            mealType = "supper",
            ingredients = new[]
            {
                new { ingredientId = tomatoesId, quantity = 30, unit = "g", note = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cream of Tomato", updated.GetProperty("title").GetString());
        Assert.Equal("supper", updated.GetProperty("mealType").GetString());
        Assert.Equal(1, updated.GetProperty("ingredients").GetArrayLength());
        Assert.Equal(2m, updated.GetProperty("ingredients")[0].GetProperty("quantity").GetDecimal());
        Assert.Equal("tbsp", updated.GetProperty("ingredients")[0].GetProperty("unit").GetString());

        // Delete
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Create_RejectsEmptyTitle()
    {
        var client = await CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = "", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsInvalidIngredient()
    {
        var client = await CreateAuthenticatedClient();
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = "Soup",
            descriptionMarkdown = (string?)null,
            ingredients = new[] { new { ingredientId = Guid.Empty, quantity = 0m, unit = "", note = (string?)null } }
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_GeneratesSlugCollisionNumber()
    {
        var client = await CreateAuthenticatedClient();
        var body = new { title = $"CollisionRecipe {Guid.NewGuid():N}", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>() };

        var first = await client.PostAsJsonAsync("/api/recipes", body);
        var second = await client.PostAsJsonAsync("/api/recipes", body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstSlug = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        var secondSlug = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("slug").GetString();
        Assert.NotEqual(firstSlug, secondSlug);
        Assert.EndsWith("-2", secondSlug);
    }

    [Fact]
    public async Task HouseMember_CanViewAnotherMembersRecipe()
    {
        var (client1, client2, _) = await TestAuth.HouseholdAsync(_factory, "recipe");

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "Private Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client2.GetAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Recipe_SurfacesCreatorToHouseMembers()
    {
        var (owner, houseId) = await TestAuth.WithHomeAsync(_factory, "recipe-creator");
        var creatorId = (await owner.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetGuid();
        var member = await TestAuth.RegisterAsync(_factory, "recipe-creator-member");
        await TestAuth.JoinHomeAsync(owner, houseId, member);

        var create = await owner.PostAsJsonAsync("/api/recipes", new
        {
            title = $"Attributed {Guid.NewGuid():N}", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal(creatorId, created.GetProperty("createdByUserId").GetGuid());

        // Another house member sees the same creator (visibility stays house-scoped).
        var memberView = await member.Client.GetFromJsonAsync<JsonElement>($"/api/recipes/{id}");
        Assert.Equal(creatorId, memberView.GetProperty("createdByUserId").GetGuid());

        var list = await member.Client.GetFromJsonAsync<JsonElement>("/api/recipes");
        var summary = list.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == id);
        Assert.Equal(creatorId, summary.GetProperty("createdByUserId").GetGuid());
    }

    [Fact]
    public async Task HouseMember_CanUpdateAnotherMembersRecipe()
    {
        var (client1, client2, _) = await TestAuth.HouseholdAsync(_factory, "recipe");

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "My Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var update = await client2.PutAsJsonAsync($"/api/recipes/{id}", new
        {
            title = "Hacked Title", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task HouseMember_CanDeleteAnotherMembersRecipe()
    {
        var (client1, client2, _) = await TestAuth.HouseholdAsync(_factory, "recipe");

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "My Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await client2.DeleteAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task List_IncludesRecipesFromHouseMembers()
    {
        var (client1, client2, _) = await TestAuth.HouseholdAsync(_factory, "recipe");
        var uniqueTitle = $"User1Recipe {Guid.NewGuid():N}";

        await client1.PostAsJsonAsync("/api/recipes", new { title = uniqueTitle, descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>() });

        var list = await client2.GetFromJsonAsync<JsonElement>("/api/recipes");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, r => r.GetProperty("title").GetString() == uniqueTitle);
    }

    [Fact]
    public async Task List_ByIngredients_RequiresEveryDistinctIngredient()
    {
        var client = await CreateAuthenticatedClient();
        var tomatoId = await CreateIngredient(client, "Filter tomato", false, null);
        var garlicId = await CreateIngredient(client, "Filter garlic", false, null);

        async Task<Guid> CreateRecipe(string title, params Guid[] ingredientIds)
        {
            var response = await client.PostAsJsonAsync("/api/recipes", new
            {
                title,
                descriptionMarkdown = (string?)null,
                mealType = "dinner",
                ingredients = ingredientIds.Select(id => new
                    { ingredientId = id, quantity = 1m, unit = "g", note = (string?)null }).ToArray()
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }

        var completeId = await CreateRecipe($"Complete soup {Guid.NewGuid():N}", tomatoId, garlicId);
        var partialId = await CreateRecipe($"Partial soup {Guid.NewGuid():N}", tomatoId);
        var query = $"/api/recipes?ingredientIds={tomatoId}&ingredientIds={garlicId}&ingredientIds={tomatoId}&mealType=dinner&search=soup";

        var result = await client.GetFromJsonAsync<JsonElement>(query);
        var items = result.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, recipe => recipe.GetProperty("id").GetGuid() == completeId);
        Assert.DoesNotContain(items, recipe => recipe.GetProperty("id").GetGuid() == partialId);
        Assert.Equal(items.Count, result.GetProperty("totalCount").GetInt32());

        var unknown = await client.GetFromJsonAsync<JsonElement>($"/api/recipes?ingredientIds={Guid.NewGuid()}");
        Assert.Equal(0, unknown.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/recipes?ingredientIds=invalid")).StatusCode);

        var oversized = string.Join("&", Enumerable.Range(0, 101).Select(_ => $"ingredientIds={Guid.NewGuid()}"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/recipes?{oversized}")).StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClient()
    {
        var client = _factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"recipe-{Guid.NewGuid():N}@example.com",
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        var body = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }

    private static async Task<Guid> CreateIngredient(HttpClient client, string label, bool isCountable, decimal? pieceSize)
    {
        var response = await client.PostAsJsonAsync("/api/ingredients", new
        {
            name = $"{label} {Guid.NewGuid():N}", measurementUnit = "g", isCountable,
            measurementUnitsPerPiece = pieceSize, caloriesPer100BaseUnits = 0m, pricePer100BaseUnits = 1m
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }
}
