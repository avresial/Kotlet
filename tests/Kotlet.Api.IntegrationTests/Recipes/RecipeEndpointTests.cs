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

        // Create
        var create = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = "Tomato Soup",
            descriptionMarkdown = "Simple **soup**.",
            ingredients = new[]
            {
                new { name = "Tomatoes", quantity = 800, unit = "g", note = (string?)"canned" },
                new { name = "Garlic", quantity = 2, unit = "cloves", note = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Tomato Soup", created.GetProperty("title").GetString());
        Assert.Equal("tomato-soup", created.GetProperty("slug").GetString());
        Assert.Equal(2, created.GetProperty("ingredients").GetArrayLength());

        // List
        var list = await client.GetFromJsonAsync<JsonElement>("/api/recipes");
        Assert.True(list.GetProperty("totalCount").GetInt32() >= 1);
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, r => r.GetProperty("id").GetGuid() == id);

        // Get detail
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{id}");
        Assert.Equal(id, detail.GetProperty("id").GetGuid());
        Assert.Equal("Simple **soup**.", detail.GetProperty("descriptionMarkdown").GetString());

        // Update
        var update = await client.PutAsJsonAsync($"/api/recipes/{id}", new
        {
            title = "Cream of Tomato",
            descriptionMarkdown = "Rich and creamy.",
            ingredients = new[]
            {
                new { name = "Tomatoes", quantity = 400, unit = "g", note = (string?)null }
            }
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cream of Tomato", updated.GetProperty("title").GetString());
        Assert.Equal(1, updated.GetProperty("ingredients").GetArrayLength());

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
            ingredients = new[] { new { name = "", quantity = (decimal?)null, unit = (string?)null, note = (string?)null } }
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
    public async Task Get_Returns404_ForOtherUsersRecipe()
    {
        var client1 = await CreateAuthenticatedClient();
        var client2 = await CreateAuthenticatedClient();

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "Private Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client2.GetAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Update_Returns404_ForOtherUsersRecipe()
    {
        var client1 = await CreateAuthenticatedClient();
        var client2 = await CreateAuthenticatedClient();

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "My Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var update = await client2.PutAsJsonAsync($"/api/recipes/{id}", new
        {
            title = "Hacked Title", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForOtherUsersRecipe()
    {
        var client1 = await CreateAuthenticatedClient();
        var client2 = await CreateAuthenticatedClient();

        var create = await client1.PostAsJsonAsync("/api/recipes", new
        {
            title = "My Recipe", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>()
        });
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client2.DeleteAsync($"/api/recipes/{id}")).StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentUsersRecipes()
    {
        var client1 = await CreateAuthenticatedClient();
        var client2 = await CreateAuthenticatedClient();
        var uniqueTitle = $"User1Recipe {Guid.NewGuid():N}";

        await client1.PostAsJsonAsync("/api/recipes", new { title = uniqueTitle, descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>() });

        var list = await client2.GetFromJsonAsync<JsonElement>("/api/recipes");
        var items = list.GetProperty("items").EnumerateArray().ToList();
        Assert.DoesNotContain(items, r => r.GetProperty("title").GetString() == uniqueTitle);
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
        return client;
    }
}
