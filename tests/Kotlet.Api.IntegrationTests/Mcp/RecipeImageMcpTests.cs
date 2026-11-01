using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kotlet.Api.Recipes;
using Kotlet.Application.Recipes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using Xunit;
using static Kotlet.Api.IntegrationTests.Mcp.McpTestHelpers;

namespace Kotlet.Api.IntegrationTests.Mcp;

public sealed class RecipeImageMcpTests(RecipeImageMcpFactory factory) : IClassFixture<RecipeImageMcpFactory>
{
    [Fact]
    public async Task Discovery_PublishesNativeFileContract()
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var result = ToolResult(await SendMcp(client, token, "tools/list", new { }));
        var tool = Assert.Single(result.GetProperty("tools").EnumerateArray(),
            tool => tool.GetProperty("name").GetString() == "upload_recipe_image");
        Assert.Equal("file", Assert.Single(tool.GetProperty("_meta").GetProperty("openai/fileParams").EnumerateArray()).GetString());
        var schema = tool.GetProperty("inputSchema");
        var fileSchema = schema.GetProperty("properties").GetProperty("file");
        Assert.Equal(new[] { "download_url", "file_id", "file_name", "mime_type" },
            fileSchema.GetProperty("properties").EnumerateObject().Select(property => property.Name).Order().ToArray());
        Assert.Equal(new[] { "download_url", "file_id" },
            fileSchema.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Order().ToArray());
        Assert.Contains("file", schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
        Assert.False(tool.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean());
        Assert.False(tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean());
        Assert.False(tool.GetProperty("annotations").GetProperty("idempotentHint").GetBoolean());
    }

    [Theory]
    [InlineData("png", DefaultProtocolVersion, true)]
    [InlineData("jpeg", DefaultProtocolVersion, true)]
    [InlineData("webp", DefaultProtocolVersion, false)]
    [InlineData("png", LegacyProtocolVersion, false)]
    public async Task Upload_PersistsFileReferenceInGallery(string format, string protocol, bool includeOptionalMetadata)
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(client);
        var file = new Dictionary<string, object>
        {
            ["file_id"] = "generated-file-id",
            ["download_url"] = $"https://images.example.test/{format}?signature=private"
        };
        if (includeOptionalMetadata)
        {
            file["mime_type"] = $"image/{format}";
            file["file_name"] = $"generated.{format}";
        }
        var result = ToolResult(await CallTool(client, token, "upload_recipe_image", new
        {
            recipeId,
            file,
            altText = "Generated recipe photo"
        }, protocol));

        var image = result.GetProperty("structuredContent");
        var imageId = image.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, imageId);
        Assert.Equal(recipeId, image.GetProperty("recipeId").GetGuid());
        Assert.Equal("Generated recipe photo", image.GetProperty("altText").GetString());
        Assert.Equal("image/webp", image.GetProperty("contentType").GetString());
        Assert.True(image.GetProperty("fileSizeBytes").GetInt64() > 0);
        Assert.DoesNotContain("signature=private", image.GetRawText());
        Assert.DoesNotContain("generated-file-id", image.GetRawText());
        var gallery = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Equal(imageId, Assert.Single(gallery.EnumerateArray()).GetProperty("id").GetGuid());
        var detail = await CallTool(client, token, "get_recipe", new { recipeId }, protocol);
        Assert.Contains(imageId.ToString(), await detail.Content.ReadAsStringAsync());
        var content = await client.GetAsync(image.GetProperty("contentUrl").GetString());
        Assert.Equal(HttpStatusCode.OK, content.StatusCode);
        Assert.Equal("image/webp", content.Content.Headers.ContentType?.MediaType);
        Assert.True(TestImages.IsWebp(await content.Content.ReadAsByteArrayAsync()));
        Assert.Null(factory.Handler.LastAuthorization);
        Assert.Null(factory.Handler.LastCookie);
        Assert.True(factory.Handler.InstrumentationSuppressed);
    }

    [Theory]
    [InlineData("/fake", "not a valid image")]
    [InlineData("/empty", "must not be empty")]
    [InlineData("/wrong-type", "Content-Type")]
    [InlineData("/no-type", "Content-Type")]
    [InlineData("/missing", "could not be downloaded")]
    [InlineData("/redirect", "Redirects are not followed")]
    [InlineData("/oversized-header", "5 MB")]
    [InlineData("/oversized-stream", "5 MB")]
    [InlineData("/transport-error", "download failed")]
    [InlineData("/timeout", "timed out")]
    [InlineData("/body-timeout", "timed out")]
    public async Task Upload_RejectsInvalidDownloadsWithoutPersistence(string path, string expected)
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(client);
        var count = factory.Handler.RequestCount;
        var result = ToolResult(await Upload(client, token, recipeId, $"https://images.example.test{path}?signature=secret"));

        AssertToolError(result, expected);
        Assert.DoesNotContain("signature=secret", result.GetRawText());
        Assert.Equal(count + 1, factory.Handler.RequestCount);
        var gallery = await client.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Empty(gallery.EnumerateArray());
    }

    [Theory]
    [InlineData("http://images.example.test/png")]
    [InlineData("https://images.example.test:444/png")]
    [InlineData("https://user:password@images.example.test/png")]
    [InlineData("https://images.example.test/png#fragment")]
    [InlineData("https://images.example.test.attacker.test/png")]
    [InlineData("https://attacker.images.example.test/png")]
    [InlineData("https://127.0.0.1/png")]
    [InlineData("https://[::ffff:127.0.0.1]/png")]
    [InlineData("https://images.example.test./png")]
    [InlineData("file:///mnt/data/generated.png")]
    [InlineData("/mnt/data/generated.png")]
    public async Task Upload_RejectsUnsafeSourceBeforeNetwork(string url)
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(client);
        var count = factory.Handler.RequestCount;
        var result = ToolResult(await Upload(client, token, recipeId, url));

        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Equal(count, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Upload_ValidatesFileMetadataAndAltText()
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(client);
        foreach (var metadata in new[]
        {
            new { file_id = "", mime_type = "image/png", file_name = "photo.png", expected = "file_id" },
            new { file_id = "file-id", mime_type = "image/jpeg", file_name = "photo.jpg", expected = "does not match" },
            new { file_id = "file-id", mime_type = "image/png", file_name = "photo.jpg", expected = "extension does not match" }
        })
        {
            var result = ToolResult(await CallTool(client, token, "upload_recipe_image", new
            {
                recipeId,
                file = new { download_url = "https://images.example.test/png", metadata.file_id, metadata.mime_type, metadata.file_name }
            }));
            AssertToolError(result, metadata.expected);
        }
        var count = factory.Handler.RequestCount;
        var invalidAlt = ToolResult(await CallTool(client, token, "upload_recipe_image", new
        {
            recipeId,
            file = new { download_url = "https://images.example.test/png", file_id = "file-id" },
            altText = new string('a', 301)
        }));
        AssertToolError(invalidAlt, "Alt text cannot exceed");
        Assert.Equal(count, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Upload_ChecksHouseholdAndMissingRecipeBeforeNetwork()
    {
        var (owner, _) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(owner);
        var (other, token) = await AuthorizeMcpClientAsync(factory);
        var count = factory.Handler.RequestCount;

        AssertToolError(ToolResult(await Upload(other, token, recipeId)), "Recipe not found");
        AssertToolError(ToolResult(await Upload(other, token, Guid.NewGuid())), "Recipe not found");
        Assert.Equal(count, factory.Handler.RequestCount);
        var gallery = await owner.GetFromJsonAsync<JsonElement>($"/api/recipes/{recipeId}/images");
        Assert.Empty(gallery.EnumerateArray());
    }

    [Fact]
    public async Task Upload_RequiresAuthentication()
    {
        var count = factory.Handler.RequestCount;
        var response = await Upload(factory.CreateClient(), "invalid", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(count, factory.Handler.RequestCount);
    }

    [Fact]
    public async Task Upload_EnforcesGalleryLimitBeforeNetwork()
    {
        var (client, token) = await AuthorizeMcpClientAsync(factory);
        var recipeId = await CreateRecipe(client);
        for (var index = 0; index < RecipeImageService.MaxImages; index++)
        {
            Assert.True(ToolResult(await Upload(client, token, recipeId)).TryGetProperty("structuredContent", out _));
        }
        var count = factory.Handler.RequestCount;
        AssertToolError(ToolResult(await Upload(client, token, recipeId)), "more than 10 images");
        Assert.Equal(count, factory.Handler.RequestCount);
    }

    private static Task<HttpResponseMessage> Upload(HttpClient client, string token, Guid recipeId,
        string url = "https://images.example.test/png") =>
        CallTool(client, token, "upload_recipe_image", new
        {
            recipeId,
            file = new { download_url = url, file_id = "generated-file-id" }
        });

    private static async Task<Guid> CreateRecipe(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/recipes", new
        {
            title = $"MCP image recipe {Guid.NewGuid():N}",
            ingredients = Array.Empty<object>()
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static void AssertToolError(JsonElement result, string expected)
    {
        Assert.True(result.GetProperty("isError").GetBoolean());
        Assert.Contains(expected, Assert.Single(result.GetProperty("content").EnumerateArray()).GetProperty("text").GetString());
    }
}

public sealed class RecipeImageMcpFactory : TestWebApplicationFactory
{
    public RecipeImageDownloadHandler Handler { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<RecipeImageFileOptions>(options => options.AllowedDownloadHosts = ["images.example.test"]);
            services.AddHttpClient<RecipeImageFileDownloader>().ConfigurePrimaryHttpMessageHandler(() => Handler);
        });
    }
}

public sealed class RecipeImageDownloadHandler : HttpMessageHandler
{
    public int RequestCount { get; private set; }
    public string? LastAuthorization { get; private set; }
    public string? LastCookie { get; private set; }
    public bool InstrumentationSuppressed { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        InstrumentationSuppressed = Sdk.SuppressInstrumentation;
        LastAuthorization = request.Headers.Authorization?.ToString();
        LastCookie = request.Headers.TryGetValues("Cookie", out var cookies) ? string.Join(";", cookies) : null;
        var path = request.RequestUri?.AbsolutePath;
        if (path == "/transport-error")
        {
            throw new HttpRequestException($"Sensitive URL: {request.RequestUri}");
        }
        if (path == "/timeout")
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        var bytes = path switch
        {
            "/jpeg" => TestImages.Jpeg(),
            "/webp" => TestImages.Webp(),
            "/empty" => [],
            "/fake" => new byte[] { 1, 2, 3 },
            _ => TestImages.Png()
        };
        HttpContent content = path switch
        {
            "/oversized-stream" => new StreamContent(new NonSeekableImageStream(new byte[RecipeImageService.MaxFileSizeBytes + 1])),
            "/body-timeout" => new StreamContent(new StalledImageStream()),
            _ => new ByteArrayContent(bytes)
        };
        if (path != "/no-type")
        {
            content.Headers.ContentType = new(path switch
            {
                "/jpeg" => "image/jpeg",
                "/webp" => "image/webp",
                "/wrong-type" => "text/html",
                _ => "image/png"
            });
        }
        if (path == "/oversized-header")
        {
            content.Headers.ContentLength = RecipeImageService.MaxFileSizeBytes + 1;
        }
        var response = new HttpResponseMessage(path switch
        {
            "/missing" => HttpStatusCode.NotFound,
            "/redirect" => HttpStatusCode.Redirect,
            _ => HttpStatusCode.OK
        })
        { Content = content };
        if (path == "/redirect")
        {
            response.Headers.Location = new("http://127.0.0.1/private");
        }
        return response;
    }

    private sealed class NonSeekableImageStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }

    private sealed class StalledImageStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }
}
