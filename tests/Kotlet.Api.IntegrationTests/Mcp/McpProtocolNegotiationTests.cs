using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

/// <summary>
/// Focused tests exercising MCP protocol negotiation (2026-07-28 and legacy 2025-11-25),
/// representative tool execution, and MCP App HTML resource serving.
/// </summary>
public sealed class McpProtocolNegotiationTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task Negotiation_WithLegacy2025Protocol_ExecutesToolsSuccessfully()
    {
        var (client, accessToken) = await McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-proto-2025");

        var response = await McpTestHelpers.SendMcp(
            client, accessToken, "tools/list", new { }, protocolVersion: McpTestHelpers.LegacyProtocolVersion);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, responseBody);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"get_recipes\"", body);
        Assert.Contains("\"get_ingredients\"", body);
    }

    [Fact]
    public async Task Negotiation_With2026ProtocolHeader_SendsVersionHeader()
    {
        var (client, accessToken) = await McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-proto-2026");

        var response = await McpTestHelpers.SendMcp(
            client, accessToken, "tools/list", new { }, protocolVersion: McpTestHelpers.DefaultProtocolVersion);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Contains("\"get_recipes\"", body);
    }

    [Fact]
    public async Task ServerDiscover_With2026Protocol_ReturnsServerMetadata()
    {
        var (client, accessToken) = await McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-discover");

        var response = await McpTestHelpers.SendMcp(
            client, accessToken, "server/discover", new { }, protocolVersion: McpTestHelpers.DefaultProtocolVersion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = McpTestHelpers.ToolResult(response);
        var supportedVersions = result.GetProperty("supportedVersions").EnumerateArray()
            .Select(version => version.GetString())
            .ToArray();
        Assert.Contains(McpTestHelpers.DefaultProtocolVersion, supportedVersions);
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task RepresentativeTool_Call_UnderLegacyProtocol_ReturnsValidToolResult()
    {
        var (client, accessToken) = await McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-rep-tool");

        var response = await McpTestHelpers.CallTool(
            client, accessToken, "get_recipes", new { search = "test" }, protocolVersion: McpTestHelpers.LegacyProtocolVersion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = McpTestHelpers.ToolResult(response);
        Assert.True(result.TryGetProperty("content", out _) || result.TryGetProperty("structuredContent", out _));
    }

    [Fact]
    public async Task McpApp_ResourceRead_UnderLegacyProtocol_PreservesUiInitializeHandshake()
    {
        var (client, accessToken) = await McpTestHelpers.AuthorizeMcpClientAsync(factory, "mcp-app-resource");

        var recipeAppResponse = await McpTestHelpers.SendMcp(
            client, accessToken, "resources/read", new { uri = "ui://kotlet/recipes-v2" },
            protocolVersion: McpTestHelpers.LegacyProtocolVersion);

        Assert.Equal(HttpStatusCode.OK, recipeAppResponse.StatusCode);
        var recipeAppBody = await recipeAppResponse.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", recipeAppBody);
        Assert.Contains("ui/initialize", recipeAppBody);
        Assert.Contains("appInfo", recipeAppBody);
        Assert.Contains("protocolVersion", recipeAppBody);

        var mealPlanAppResponse = await McpTestHelpers.SendMcp(
            client, accessToken, "resources/read", new { uri = "ui://kotlet/meal-plan-v1" },
            protocolVersion: McpTestHelpers.LegacyProtocolVersion);

        Assert.Equal(HttpStatusCode.OK, mealPlanAppResponse.StatusCode);
        var mealPlanAppBody = await mealPlanAppResponse.Content.ReadAsStringAsync();
        Assert.Contains("text/html;profile=mcp-app", mealPlanAppBody);
        Assert.Contains("ui/initialize", mealPlanAppBody);
        Assert.Contains("appInfo", mealPlanAppBody);
        Assert.Contains("protocolVersion", mealPlanAppBody);
    }
}
