using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Ai;

public sealed class AiProviderEndpointTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Configuration_IsUserScopedAndNeverReturnsApiKey()
    {
        var owner = await TestAuth.RegisterAsync(factory, "ai-provider-owner");
        var other = await TestAuth.RegisterAsync(factory, "ai-provider-other");

        var savedResponse = await owner.Client.PutAsJsonAsync("/api/ai-provider", new
        {
            providerName = "OpenRouter",
            baseUrl = "https://openrouter.ai/api/v1",
            apiKey = "secret-key",
            defaultModel = "openai/gpt-4.1-mini",
            isEnabled = true
        });
        Assert.Equal(HttpStatusCode.OK, savedResponse.StatusCode);
        var saved = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(saved.GetProperty("hasApiKey").GetBoolean());
        Assert.False(saved.TryGetProperty("apiKey", out _));

        Assert.Equal(HttpStatusCode.NotFound, (await other.Client.GetAsync("/api/ai-provider")).StatusCode);

        var updatedResponse = await owner.Client.PutAsJsonAsync("/api/ai-provider", new
        {
            providerName = "OpenRouter",
            baseUrl = "https://openrouter.ai/api/v1",
            defaultModel = "openai/gpt-4.1",
            isEnabled = true
        });
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(updated.GetProperty("hasApiKey").GetBoolean());
        Assert.False(updated.TryGetProperty("apiKey", out _));

        Assert.Equal(HttpStatusCode.NoContent, (await owner.Client.DeleteAsync("/api/ai-provider")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.Client.GetAsync("/api/ai-provider")).StatusCode);
    }
}
