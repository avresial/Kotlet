using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

public sealed class McpDiscoveryEndpointTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Discovery_ReturnsPublicDocument_PointingAtConfiguredEndpoints()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/.well-known/mcp.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Kotlet", document.GetProperty("name").GetString());
        // Pinned to the assembly <Version>; bump both together so metadata-caching clients refresh.
        Assert.Equal("1.4.0", document.GetProperty("version").GetString());
        Assert.Equal("http://localhost/mcp", document.GetProperty("mcp_endpoint").GetString());
        Assert.Equal("http://localhost/connect/authorize", document.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/connect/token", document.GetProperty("token_endpoint").GetString());
        Assert.Equal("kotlet-mcp-tests", document.GetProperty("client_id").GetString());
        Assert.Contains("mcp", document.GetProperty("scopes_supported").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task Mcp_WithInvalidBearerToken_ReturnsUnauthorizedWithResourceMetadata()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method = "tools/list", @params = new { } })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-token");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate,
            header => header.Parameter?.Contains("resource_metadata", StringComparison.Ordinal) == true);
    }
}
