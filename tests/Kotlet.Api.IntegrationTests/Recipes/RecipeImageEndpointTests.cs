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
        var first = await Upload(client, recipeId, "first.png", "image/png", TestImages.Png(), "First");
        var second = await Upload(client, recipeId, "second.webp", "image/webp", TestImages.Webp(), null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondJson = await second.Content.ReadFromJsonAsync<JsonElement>();
        var firstId = firstJson.GetProperty("id").GetGuid();
        var secondId = secondJson.GetProperty("id").GetGuid();
        Assert.Equal("image/webp", firstJson.GetProperty("contentType").GetString());
        Assert.Equal("first.webp", firstJson.GetProperty("fileName").GetString());
        Assert.Equal("image/webp", secondJson.GetProperty("contentType").GetString());

        var images = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.False(images[0].TryGetProperty("content", out _));

        var content = await client.GetAsync($"/api/recipes/{recipeId}/images/{firstId}/content");
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);
        Assert.True(TestImages.IsWebp(await content.Content.ReadAsByteArrayAsync()));

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
        var upload = await Upload(owner, recipeId, "photo.png", "image/png", TestImages.Png(), null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/recipes/{recipeId}/images")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, recipeId, "bad.gif", "image/gif", TestImages.Png(), null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, recipeId, "empty.png", "image/png", [], null)).StatusCode);
        // Declared type is fine but the bytes are not an image: rejected by decoding, not by the extension check.
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(owner, recipeId, "fake.png", "image/png", [1, 2, 3], null)).StatusCode);
    }

    [Fact]
    public async Task Upload_PersistsExternalImageAttribution()
    {
        var client = await AuthenticatedClient();
        var recipeId = await CreateRecipe(client);

        var response = await Upload(client, recipeId, "generated.webp", "image/webp", TestImages.Webp(), "Pasta",
            "Pexels", "42", "https://www.pexels.com/photo/42", "Ada", "https://pexels.com/@ada");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var image = await response.Content.ReadFromJsonAsync<JsonElement>();
        var source = image.GetProperty("source");
        Assert.Equal("Pexels", source.GetProperty("provider").GetString());
        Assert.Equal("Ada", source.GetProperty("authorName").GetString());
        Assert.Equal("https://www.pexels.com/photo/42", source.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Import_ReturnsBadRequestForMissingSelection()
    {
        var client = await AuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/recipes/images/import", new { provider = "", externalImageId = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_ReturnsServiceUnavailableWhenProviderIsNotConfigured()
    {
        var client = await AuthenticatedClient();

        var response = await client.PostAsJsonAsync("/api/recipes/images/import", new { provider = "Pexels", externalImageId = "42" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CanViewRecipeImageContentButCannotUpload()
    {
        var owner = await AuthenticatedClient();
        var recipeId = await CreateRecipe(owner);
        var upload = await Upload(owner, recipeId, "photo.png", "image/png", TestImages.Png(), null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var anonymous = factory.CreateClient();
        var content = await anonymous.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content");
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.True(TestImages.IsWebp(await content.Content.ReadAsByteArrayAsync()));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Upload(anonymous, recipeId, "nope.png", "image/png", TestImages.Png(), null)).StatusCode);
    }

    [Fact]
    public async Task DeletingRecipe_CascadesImages()
    {
        var client = await AuthenticatedClient();
        var recipeId = await CreateRecipe(client);
        var upload = await Upload(client, recipeId, "photo.jpg", "image/jpeg", TestImages.Jpeg(), null);
        var imageId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/recipes/{recipeId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recipes/{recipeId}/images/{imageId}/content")).StatusCode);
    }

    private static async Task<HttpResponseMessage> Upload(HttpClient client, Guid recipeId, string name, string type, byte[] bytes,
        string? alt, string? sourceProvider = null, string? sourceExternalId = null, string? sourceUrl = null,
        string? sourceAuthorName = null, string? sourceAuthorUrl = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(file, "file", name);
        if (alt is not null) form.Add(new StringContent(alt, Encoding.UTF8), "altText");
        Add(sourceProvider, "sourceProvider");
        Add(sourceExternalId, "sourceExternalId");
        Add(sourceUrl, "sourceUrl");
        Add(sourceAuthorName, "sourceAuthorName");
        Add(sourceAuthorUrl, "sourceAuthorUrl");
        return await client.PostAsync($"/api/recipes/{recipeId}/images", form);

        void Add(string? value, string name)
        {
            if (value is not null) form.Add(new StringContent(value, Encoding.UTF8), name);
        }
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
