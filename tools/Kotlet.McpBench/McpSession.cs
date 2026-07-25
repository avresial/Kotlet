using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Kotlet.McpBench;

/// <summary>One authenticated MCP connection, plus the JSON-RPC plumbing used to time calls.</summary>
public sealed class McpSession(HttpClient client, string accessToken, string protocolVersion)
{
    private int requestId;

    public static async Task<McpSession> ConnectAsync(
        HttpClient client, Credentials credentials, string resource, string clientId, string protocolVersion)
    {
        // The authorization endpoint authenticates from the refresh cookie that signing in
        // sets, so the cookie-carrying client is what drives the OAuth handshake below.
        var bearer = await SignInAsync(client, credentials);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        var accessToken = await AuthorizeAsync(client, resource, clientId);
        return new McpSession(client, accessToken, protocolVersion);
    }

    /// <summary>Sends one JSON-RPC request and reports what the wire and the clock saw.</summary>
    public async Task<McpCallResult> SendAsync(string method, object parameters, DbQueryCounter? counter = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", protocolVersion);
        request.Content = JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = Interlocked.Increment(ref requestId),
            method,
            @params = parameters
        });

        var queriesBefore = counter?.Count ?? 0;
        var stopwatch = Stopwatch.StartNew();
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        stopwatch.Stop();
        var queries = counter is null ? (int?)null : counter.Count - queriesBefore;

        var payload = StripEventStreamFraming(body);
        return new McpCallResult(
            method,
            (int)response.StatusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            payload.Length,
            queries,
            payload);
    }

    public Task<McpCallResult> CallToolAsync(string name, object arguments, DbQueryCounter? counter = null) =>
        SendAsync("tools/call", new { name, arguments }, counter);

    /// <summary>Server-Sent-Events framing wraps the JSON body in a "data: " line.</summary>
    private static string StripEventStreamFraming(string body)
    {
        var index = body.IndexOf("data: ", StringComparison.Ordinal);
        return index < 0 ? body.Trim() : body[(index + "data: ".Length)..].Trim();
    }

    private static async Task<string> SignInAsync(HttpClient client, Credentials credentials)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = credentials.Email,
            password = credentials.Password
        });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // The MCP tools read the household from the token, so an existing account has to
        // select one before the OAuth token carries a house_id claim.
        var houses = (await client.GetFromJsonAsync<JsonElement>("/api/houses")).EnumerateArray().ToArray();
        if (houses.Length == 0)
            throw new InvalidOperationException("The account has no household; MCP tools need one.");
        var house = houses.FirstOrDefault(
            candidate => candidate.TryGetProperty("isDefault", out var isDefault) && isDefault.GetBoolean(),
            houses[0]);

        var switched = await client.PostAsync($"/api/houses/{house.GetProperty("id").GetString()}/switch", content: null);
        switched.EnsureSuccessStatusCode();
        return (await switched.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("accessToken").GetString()!;
    }

    private static async Task<string> AuthorizeAsync(HttpClient client, string resource, string clientId)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var authorizeUrl = QueryHelpers.AddQueryString("/connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = clientId,
            ["response_type"] = "code",
            ["redirect_uri"] = "http://127.0.0.1/callback",
            ["scope"] = "mcp",
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = resource
        });

        var authorized = await client.GetAsync(authorizeUrl);
        if (authorized.Headers.Location is not { } location)
            throw new InvalidOperationException(
                $"The authorization endpoint returned {(int)authorized.StatusCode} without a redirect. " +
                "Check that the client id and resource match the server's OAuth configuration.");
        var code = QueryHelpers.ParseQuery(location.Query)["code"].SingleOrDefault()
            ?? throw new InvalidOperationException($"No authorization code in the redirect to {location}.");

        var tokenResponse = await client.PostAsync("/connect/token", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = "http://127.0.0.1/callback",
                ["code_verifier"] = verifier,
                ["resource"] = resource
            }));
        tokenResponse.EnsureSuccessStatusCode();
        return (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("access_token").GetString()!;
    }
}

public sealed record Credentials(string Email, string Password);

public sealed record McpCallResult(
    string Method,
    int StatusCode,
    double ElapsedMs,
    int WireBytes,
    int? DbQueries,
    string Payload)
{
    public JsonElement Result
    {
        get
        {
            using var document = JsonDocument.Parse(Payload);
            if (document.RootElement.TryGetProperty("error", out var error))
                throw new InvalidOperationException($"MCP error: {error.GetRawText()}");
            return document.RootElement.GetProperty("result").Clone();
        }
    }

    /// <summary>
    /// Splits a tools/call result into the text block agents read and the structuredContent
    /// copy. When both carry the same data, the smaller of the two is redundant payload.
    /// </summary>
    public (int TextBytes, int StructuredBytes) ContentSplit()
    {
        var result = Result;
        var text = result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array
            ? content.EnumerateArray().Sum(block =>
                block.TryGetProperty("text", out var value) ? value.GetString()?.Length ?? 0 : 0)
            : 0;
        var structured = result.TryGetProperty("structuredContent", out var structuredContent)
            ? structuredContent.GetRawText().Length
            : 0;
        return (text, structured);
    }
}
