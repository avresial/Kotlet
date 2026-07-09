using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Recipes;

public sealed class RecipeImageEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Image_CanBeUploadedListedServedUpdatedReorderedAndDeleted()
    {
        var client = await AuthenticatedClient();
        var recipeId = await CreateRecipe(client);
        var first = await Upload(client, recipeId, "first.png", "image/png", [1, 2, 3], "First");
        var second = await Upload(client, recipeId, "second.webp", "image/webp", [4, 5], null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondJson = await second.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = firstJson.GetProperty("id").GetGuid();
        var secondId = secondJson.GetProperty("id").GetGuid();

        var images = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.False(images[0].TryGetProperty("content", out _));

        var content = await client.GetAsync($"/api/recipes/{recipeId}/images/{firstId}/content");
        Assert.Equal("image/png", content.Content.Headers.ContentType?.MediaType);
        Assert.Equal([1, 2, 3], await content.Content.ReadAsByteArrayAsync());

        var patch = await client.PatchAsJsonAsync($"/api/recipes/{recipeId}/images/{firstId}", new { altText = "Updated" });
        Assert.Equal("Updated", (await patch.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("altText").GetString());

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/recipes/{recipeId}/images/order",
            new { imageIds = new[] { secondId, firstId } })).StatusCode);
        images = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Equal(secondId, images[0].GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{recipeId}/images/{secondId}")).StatusCode);
        images = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Equal(0, images[0].GetProperty("sortOrder").GetInt32());
    }

    [Fact]
    public async Task Images_AreSharedWithinHouseAndValidated()
    {
        var (owner, other, _) = await TestAuth.HouseholdAsync(factory, "images");
        var recipeId = await CreateRecipe(owner);
        var upload = await Upload(owner, recipeId, "photo.png", "image/png", [1], null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/recipes/{recipeId}/images")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, recipeId, "bad.gif", "image/gif", [1], null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, recipeId, "empty.png", "image/png", [], null)).StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CanViewRecipeImageContentButCannotUpload()
    {
        var owner = await AuthenticatedClient();
        var recipeId = await CreateRecipe(owner);
        var upload = await Upload(owner, recipeId, "photo.png", "image/png", [1, 2, 3], null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        var content = await anonymous.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal([1, 2, 3], await content.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(anonymous, recipeId, "nope.png", "image/png", [1], null)).StatusCode);
    }

    [Fact]
    public async Task DeletingRecipe_CascadesImages()
    {
        var client = await AuthenticatedClient();
        var recipeId = await CreateRecipe(client);
        var upload = await Upload(client, recipeId, "photo.jpg", "image/jpeg", [1, 2], null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{recipeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content")).StatusCode);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid recipeId, string name, string type, byte[] bytes, string? alt)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", name);
        if (alt is not null) form.Add(new StringContent(alt, Encoding.UTF8), "altText");
        return await client.PostAsync($"/api/recipes/{recipeId}/images", form);
    }

    private static async Task<Guid> CreateRecipe(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new { title = $"Image recipe {Guid.NewGuid():N}", descriptionMarkdown = (string?)null, ingredients = Array.Empty<object>() });
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> AuthenticatedClient()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email = $"images-{Guid.NewGuid():N}@example.com", password = "Password1!", confirmPassword = "Password1!" });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        await TestAuth.CreateHomeAsync(client);
        return client;
    }
}
