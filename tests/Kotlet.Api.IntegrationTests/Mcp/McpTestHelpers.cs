using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

/// <summary>
/// Shared helper methods for MCP integration tests, supporting parameterized MCP protocol versions
/// (defaulting to 2026-07-28 while supporting legacy 2025-11-25), OAuth authorization, request sending,
/// and SSE response parsing.
/// </summary>
public static class McpTestHelpers
{
    public const string DefaultProtocolVersion = "2026-07-28";
    public const string LegacyProtocolVersion = "2025-11-25";
    private const string ProtocolVersionMetadataKey = "io.modelcontextprotocol/protocolVersion";

    /// <summary>
    /// Registers a user with a home and runs the OAuth PKCE flow for an MCP-scoped token.
    /// </summary>
    public static async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync(
        TestWebApplicationFactory factory,
        string emailPrefix = "mcp")
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"{emailPrefix}-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var registrationToken = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registrationToken);
        var house = await client.PostAsJsonAsync("/api/houses", new { name = $"{emailPrefix} home" });
        house.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await house.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetProperty("accessToken").GetString());

        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorization = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "kotlet-mcp-tests",
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = "http://localhost/mcp"
        });
        var authorizeResponse = await client.GetAsync(authorization);
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        var code = Assert.Single(QueryHelpers.ParseQuery(authorizeResponse.Headers.Location!.Query)["code"]);
        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = "kotlet-mcp-tests",
            ["code"] = code!,
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["code_verifier"] = verifier,
            ["resource"] = "http://localhost/mcp"
        }));
        tokenResponse.EnsureSuccessStatusCode();
        var accessToken = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
        return (client, accessToken);
    }

    public static Guid ExtractGuidAfter(string body, string marker)
    {
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Marker '{marker}' not found in: {body}");
        start += marker.Length;
        return Guid.Parse(body.Substring(start, 36));
    }

    public static Task<HttpResponseMessage> CallTool(
        HttpClient client, string accessToken, string name, object arguments,
        string protocolVersion = DefaultProtocolVersion, string? language = null)
        => SendMcp(client, accessToken, "tools/call", new { name, arguments }, protocolVersion, language);

    public static Task<HttpResponseMessage> SendMcp(
        HttpClient client, string accessToken, string method, object parameters,
        string protocolVersion = DefaultProtocolVersion, string? language = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", protocolVersion);
        if (protocolVersion == DefaultProtocolVersion)
        {
            request.Headers.Add("Mcp-Method", method);
            if (GetRequestName(method, parameters) is { } requestName)
            {
                request.Headers.Add("Mcp-Name", requestName);
            }
        }
        if (language is not null)
        {
            request.Headers.AcceptLanguage.ParseAdd(language);
        }
        request.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 1,
            method,
            @params = AddProtocolMetadata(parameters, protocolVersion)
        });
        return client.SendAsync(request);
    }

    private static string? GetRequestName(string method, object parameters)
    {
        var parametersElement = JsonSerializer.SerializeToElement(parameters);
        return method switch
        {
            "tools/call" or "prompts/get" when parametersElement.TryGetProperty("name", out var name)
                => name.GetString(),
            "resources/read" when parametersElement.TryGetProperty("uri", out var uri)
                => uri.GetString(),
            _ => null
        };
    }

    private static object AddProtocolMetadata(object parameters, string protocolVersion)
    {
        if (protocolVersion != DefaultProtocolVersion)
            return parameters;

        var parametersObject = JsonSerializer.SerializeToNode(parameters)?.AsObject()
            ?? new JsonObject();
        var metadata = parametersObject["_meta"] as JsonObject ?? new JsonObject();
        metadata[ProtocolVersionMetadataKey] = protocolVersion;
        metadata["io.modelcontextprotocol/clientCapabilities"] = new JsonObject();
        metadata["io.modelcontextprotocol/clientInfo"] = new JsonObject
        {
            ["name"] = "kotlet-integration-tests",
            ["version"] = "1.0.0"
        };
        parametersObject["_meta"] = metadata;
        return parametersObject;
    }

    public static JsonElement ToolResult(HttpResponseMessage response, string property = "result")
    {
        using var reader = new StreamReader(response.Content.ReadAsStream());
        var body = reader.ReadToEnd();
        return ReadSseResult(body, property);
    }

    public static JsonElement ReadSseResult(string body, string property = "result")
    {
        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data:", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine[5..].Trim());
        return document.RootElement.GetProperty(property).Clone();
    }

    public static void AssertShortText(JsonElement result, string expected)
    {
        var content = Assert.Single(result.GetProperty("content").EnumerateArray());
        var text = content.GetProperty("text").GetString();
        Assert.Contains(expected, text!);
        Assert.True(text!.Length < 120);
        Assert.DoesNotContain('{', text);
    }

    public static void AssertDoesNotContainKey(JsonElement value, string key) =>
        Assert.False(value.TryGetProperty(key, out _),
            $"'{key}' must not be present in the recipe presentation payload.");
}
