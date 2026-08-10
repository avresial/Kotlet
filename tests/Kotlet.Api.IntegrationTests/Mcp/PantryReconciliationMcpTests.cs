using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Xunit;

namespace Kotlet.Api.IntegrationTests.Mcp;

public sealed class PantryReconciliationMcpTests(TestWebApplicationFactory factory)
    : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task ResolveAndReconcile_PreservesCategoriesAndAppliesAnIdempotentMerge()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredientName = $"Fridge milk {Guid.NewGuid():N}";
        var ingredientId = await CreateIngredientAsync(client, accessToken, ingredientName, "ml", appendUnique: false);

        var resolution = await CallToolAsync(client, accessToken, "pantry.resolve_observations", new
        {
            request = new
            {
                observations = new object[]
                {
                    new
                    {
                        observationId = "milk-1",
                        rawPhrase = "half bottle of fridge milk",
                        normalizedName = ingredientName,
                        estimatedQuantity = 500,
                        unit = "ml",
                        identityConfidence = 0.95,
                        quantityConfidence = 0.9,
                        evidence = new[] { "photo-1", "photo-2" }
                    },
                    new
                    {
                        observationId = "unknown-1",
                        rawPhrase = "blue mystery carton",
                        normalizedName = "blue mystery carton",
                        identityConfidence = 0.95
                    },
                    new
                    {
                        observationId = "uncertain-1",
                        rawPhrase = ingredientName,
                        normalizedName = ingredientName,
                        identityConfidence = 0.4
                    }
                }
            }
        });

        var resolutionData = resolution.GetProperty("structuredContent");
        Assert.Equal("Success", resolutionData.GetProperty("status").GetString());
        Assert.True(resolutionData.GetProperty("matched").GetArrayLength() == 1, resolutionData.GetRawText());
        Assert.True(resolutionData.GetProperty("unmatched").GetArrayLength() == 1, resolutionData.GetRawText());
        Assert.True(resolutionData.GetProperty("ambiguous").GetArrayLength() == 1, resolutionData.GetRawText());
        var pantryVersion = resolutionData.GetProperty("pantryVersion").GetInt64();

        var operationId = $"scan-{Guid.NewGuid():N}";
        var reconcile = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId,
                expectedPantryVersion = pantryVersion,
                mode = "merge",
                scope = new { location = "fridge", coverage = "partial" },
                items = new[]
                {
                    new
                    {
                        observationId = "milk-1",
                        itemId = ingredientId,
                        itemType = "ingredient",
                        observedQuantity = 500,
                        observedUnit = "ml",
                        normalizedQuantity = (decimal?)null,
                        normalizedUnit = (string?)null,
                        quantityConfidence = 0.9,
                        identityConfidence = 0.95,
                        packageDescription = "half bottle"
                    }
                },
                unmatched = new[]
                {
                    new
                    {
                        observationId = "unknown-1",
                        rawPhrase = "blue mystery carton",
                        normalizedPhrase = "BLUE MYSTERY CARTON",
                        reason = "No catalogue item was similar enough to resolve safely.",
                        identityConfidence = 0.95
                    }
                },
                ambiguous = new[]
                {
                    new
                    {
                        observationId = "uncertain-1",
                        rawPhrase = ingredientName,
                        normalizedPhrase = ingredientName.ToUpperInvariant(),
                        candidates = new[]
                        {
                            new
                            {
                                itemId = ingredientId,
                                itemType = "ingredient",
                                name = ingredientName,
                                measurementUnit = "ml",
                                matchConfidence = 1.0
                            }
                        },
                        identityConfidence = 0.4
                    }
                },
                unrecognizedCount = 2
            }
        });

        Assert.True(reconcile.TryGetProperty("structuredContent", out var reconcileData), reconcile.GetRawText());
        Assert.Equal("Success", reconcileData.GetProperty("status").GetString());
        Assert.Equal(1, reconcileData.GetProperty("added").GetArrayLength());
        Assert.Equal(1, reconcileData.GetProperty("unmatched").GetArrayLength());
        Assert.Equal(1, reconcileData.GetProperty("ambiguous").GetArrayLength());
        Assert.Equal(2, reconcileData.GetProperty("unrecognizedCount").GetInt32());
        Assert.Equal("ui://kotlet/data-v3", reconcileData.GetProperty("uiResource").GetString());
        Assert.Contains("1 added", reconcile.GetProperty("content")[0].GetProperty("text").GetString());

        var duplicateIngredientId = await CreateIngredientAsync(client, accessToken, "Fridge rice", "g");
        var duplicate = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId = "duplicate-observations",
                expectedPantryVersion = reconcileData.GetProperty("pantryVersion").GetInt64(),
                mode = "merge",
                scope = new { location = "fridge", coverage = "partial" },
                items = new[]
                {
                    Item("rice-photo-1", duplicateIngredientId, 100, "g", 0.9m),
                    Item("rice-photo-2", duplicateIngredientId, 500, "g", 0.95m)
                }
            }
        });
        var duplicateData = duplicate.GetProperty("structuredContent");
        Assert.Equal(1, duplicateData.GetProperty("added").GetArrayLength());
        Assert.Equal(2, duplicateData.GetProperty("added")[0].GetProperty("observationIds").GetArrayLength());
        Assert.Equal(500, duplicateData.GetProperty("added")[0].GetProperty("newQuantity").GetDecimal());

        var stale = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId = "stale-operation",
                expectedPantryVersion = pantryVersion,
                mode = "merge",
                scope = new { location = "fridge", coverage = "partial" },
                items = Array.Empty<object>()
            }
        });
        Assert.Equal("Conflict", stale.GetProperty("structuredContent").GetProperty("status").GetString());

        var repeated = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId,
                expectedPantryVersion = pantryVersion,
                mode = "merge",
                scope = new { location = "fridge", coverage = "partial" },
                items = new[]
                {
                    new
                    {
                        observationId = "milk-1",
                        itemId = ingredientId,
                        itemType = "ingredient",
                        observedQuantity = 500,
                        observedUnit = "ml",
                        normalizedQuantity = (decimal?)null,
                        normalizedUnit = (string?)null,
                        quantityConfidence = 0.9,
                        identityConfidence = 0.95,
                        packageDescription = "half bottle"
                    }
                },
                unrecognizedCount = 2
            }
        });
        Assert.Equal(reconcileData.GetProperty("pantryVersion").GetInt64(),
            repeated.GetProperty("structuredContent").GetProperty("pantryVersion").GetInt64());

        var pantry = await client.GetFromJsonAsync<JsonElement[]>("/api/pantry");
        var stored = Assert.Single(pantry!, item => item.GetProperty("ingredientId").GetGuid() == ingredientId);
        Assert.Equal(500, stored.GetProperty("quantity").GetDecimal());
        Assert.Equal("half bottle", stored.GetProperty("packageDescription").GetString());
        var duplicateStored = Assert.Single(pantry!, item => item.GetProperty("ingredientId").GetGuid() == duplicateIngredientId);
        Assert.Equal(500, duplicateStored.GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public async Task Reconcile_LowConfidenceDoesNotChangeQuantity_AndReplacementCanBeUndone()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var milkId = await CreateIngredientAsync(client, accessToken, "Undo milk", "ml");
        var flourId = await CreateIngredientAsync(client, accessToken, "Undo flour", "g");

        var firstVersion = await GetPantryVersionAsync(client, accessToken);
        var first = await ReconcileAsync(client, accessToken, firstVersion, "initial", new[]
        {
            Item("milk", milkId, 500, "ml", 0.95m)
        });
        var currentVersion = first.GetProperty("pantryVersion").GetInt64();

        var review = await ReconcileAsync(client, accessToken, currentVersion, "review", new[]
        {
            Item("milk-low-confidence", milkId, 100, "ml", 0.4m)
        });
        Assert.Equal(1, review.GetProperty("needsReview").GetArrayLength());
        var afterReview = await client.GetFromJsonAsync<JsonElement[]>("/api/pantry");
        Assert.Equal(500, Assert.Single(afterReview!).GetProperty("quantity").GetDecimal());

        currentVersion = review.GetProperty("pantryVersion").GetInt64();
        var second = await ReconcileAsync(client, accessToken, currentVersion, "add-second", new[]
        {
            Item("flour", flourId, 800, "g", 0.95m)
        });
        currentVersion = second.GetProperty("pantryVersion").GetInt64();

        var replacement = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId = "replace-all",
                expectedPantryVersion = currentVersion,
                mode = "replace_location",
                scope = new { location = "refrigerator", coverage = "full" },
                confirm = true,
                items = new[]
                {
                    Item("milk-replacement", milkId, 450, "ml", 0.95m)
                }
            }
        });
        var replacementData = replacement.GetProperty("structuredContent");
        Assert.Equal(1, replacementData.GetProperty("removed").GetArrayLength());
        var undoToken = replacementData.GetProperty("undoToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(undoToken));

        var undo = await CallToolAsync(client, accessToken, "pantry.undo_reconcile", new { undoToken });
        var undoData = undo.GetProperty("structuredContent");
        Assert.Equal("Success", undoData.GetProperty("status").GetString());
        var restored = await client.GetFromJsonAsync<JsonElement[]>("/api/pantry");
        Assert.Equal(2, restored!.Length);
        Assert.Contains(restored, item => item.GetProperty("ingredientId").GetGuid() == flourId);
    }

    [Fact]
    public async Task Reconcile_RejectsPartialDestructiveModesAndUnsafeConversions()
    {
        var (client, accessToken) = await AuthorizeMcpClientAsync();
        var ingredientId = await CreateIngredientAsync(client, accessToken, "Safety flour", "g");
        var version = await GetPantryVersionAsync(client, accessToken);

        var partial = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId = "partial-replace",
                expectedPantryVersion = version,
                mode = "replace_location",
                scope = new { location = "fridge", coverage = "partial" },
                confirm = true,
                items = Array.Empty<object>()
            }
        });
        var partialErrors = partial.GetProperty("structuredContent").GetProperty("validationErrors");
        Assert.Contains("full", partialErrors.GetProperty("scope.coverage")[0].GetString(), StringComparison.OrdinalIgnoreCase);

        var unsafeConversion = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId = "unsafe-unit",
                expectedPantryVersion = version,
                mode = "merge",
                scope = new { location = "cabinet", coverage = "partial" },
                items = new[]
                {
                    new
                    {
                        observationId = "flour-package",
                        itemId = ingredientId,
                        itemType = "ingredient",
                        observedQuantity = 1,
                        observedUnit = "package",
                        normalizedQuantity = (decimal?)null,
                        normalizedUnit = (string?)null,
                        quantityConfidence = 0.95,
                        identityConfidence = 0.95
                    }
                }
            }
        });
        var unsafeErrors = unsafeConversion.GetProperty("structuredContent").GetProperty("validationErrors").EnumerateObject().Select(property => property.Name).ToArray();
        Assert.Contains("items[0].observedUnit", unsafeErrors);
    }

    private static object Item(string observationId, Guid ingredientId, decimal quantity, string unit, decimal confidence) => new
    {
        observationId,
        itemId = ingredientId,
        itemType = "ingredient",
        observedQuantity = quantity,
        observedUnit = unit,
        normalizedQuantity = (decimal?)null,
        normalizedUnit = (string?)null,
        quantityConfidence = confidence,
        identityConfidence = 0.95m
    };

    private async Task<JsonElement> ReconcileAsync(
        HttpClient client,
        string accessToken,
        long pantryVersion,
        string operationId,
        IReadOnlyList<object> items)
    {
        var response = await CallToolAsync(client, accessToken, "pantry.reconcile", new
        {
            request = new
            {
                operationId,
                expectedPantryVersion = pantryVersion,
                mode = "merge",
                scope = new { location = "refrigerator", coverage = "partial" },
                items
            }
        });
        Assert.True(response.TryGetProperty("structuredContent", out var structuredContent), response.GetRawText());
        Assert.True(structuredContent.TryGetProperty("status", out _), response.GetRawText());
        return structuredContent;
    }

    private async Task<Guid> CreateIngredientAsync(
        HttpClient client,
        string accessToken,
        string name,
        string measurementUnit,
        bool appendUnique = true)
    {
        var persistedName = appendUnique ? $"{name} {Guid.NewGuid():N}" : name;
        var response = await CallToolAsync(client, accessToken, "create_ingredient", new
        {
            request = new { name = persistedName, measurementUnit, caloriesPer100BaseUnits = 10 }
        });
        return response.GetProperty("structuredContent").GetProperty("ingredientId").GetGuid();
    }

    private async Task<long> GetPantryVersionAsync(HttpClient client, string accessToken)
    {
        var response = await CallToolAsync(client, accessToken, "pantry.resolve_observations", new
        {
            request = new
            {
                observations = new[]
                {
                    new { observationId = "version-check", rawPhrase = "not a catalogue item", normalizedName = "" }
                }
            }
        });
        return response.GetProperty("structuredContent").GetProperty("pantryVersion").GetInt64();
    }

    private async Task<JsonElement> CallToolAsync(
        HttpClient client,
        string accessToken,
        string name,
        object arguments)
    {
        using var response = await SendMcpAsync(client, accessToken, "tools/call", new { name, arguments });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var dataLine = body.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("data:", StringComparison.Ordinal));
        using var document = JsonDocument.Parse(dataLine[5..].Trim());
        var result = document.RootElement.GetProperty("result").Clone();
        return result;
    }

    private async Task<(HttpClient Client, string AccessToken)> AuthorizeMcpClientAsync()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var email = $"mcp-pantry-{Guid.NewGuid():N}@example.com";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Password1!",
            confirmPassword = "Password1!"
        });
        registration.EnsureSuccessStatusCode();
        var registrationBody = await registration.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", registrationBody.GetProperty("accessToken").GetString());
        var house = await client.PostAsJsonAsync("/api/houses", new { name = "MCP pantry home" });
        house.EnsureSuccessStatusCode();
        var houseBody = await house.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", houseBody.GetProperty("token").GetProperty("accessToken").GetString());

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
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (client, tokenBody.GetProperty("access_token").GetString()!);
    }

    private static async Task<HttpResponseMessage> SendMcpAsync(
        HttpClient client,
        string accessToken,
        string method,
        object parameters)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        request.Headers.Add("MCP-Protocol-Version", "2025-11-25");
        return await client.SendAsync(request);
    }
}
