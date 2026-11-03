using System.Globalization;
using System.Net;
using System.Text.Json;
using Kotlet.Application.Products;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Kotlet.Infrastructure.Products;

// A single process-wide instance bounds both outbound requests and cached public product data.
internal sealed class FoodDataCentralClient(IHttpClientFactory httpClients, IConfiguration configuration, TimeProvider clock)
    : IFoodDataCentralClient, IDisposable
{
    internal const string HttpClientName = "FoodDataCentral";
    private readonly string? _apiKey = configuration["FoodDataCentral:ApiKey"]?.Trim();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _requests = new();
    private DateTimeOffset _retryAfter;

    public async Task<ProductDto?> LookupAsync(string normalizedBarcode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(_apiKey) || !Barcode.TryNormalize(normalizedBarcode, out var barcode))
        {
            return null;
        }
        if (_cache.TryGetValue(barcode, out ProductLookupResult? cached))
        {
            return cached?.Product;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(barcode, out cached))
            {
                return cached?.Product;
            }
            ProductLookupResult result;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                result = await FetchAsync(barcode, timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = new("unavailable");
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or IOException or FormatException)
            {
                result = new("unavailable");
            }
            var lifetime = result.Status switch
            {
                "food_data_central" => TimeSpan.FromHours(24),
                "not_found" => TimeSpan.FromMinutes(15),
                _ => TimeSpan.FromSeconds(30)
            };
            _cache.Set(barcode, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime, Size = 1 });
            return result.Product;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ProductLookupResult> FetchAsync(string barcode, CancellationToken cancellationToken)
    {
        // USDA indexes UPC-A as 12 digits; the equivalent OFF representation has 13 digits.
        var code = barcode.StartsWith("000000", StringComparison.Ordinal) ? barcode[^8..]
            : barcode.StartsWith("00", StringComparison.Ordinal) ? barcode[^12..]
            : barcode.StartsWith('0') ? barcode[^13..] : barcode;
        using var client = httpClients.CreateClient(HttpClientName);
        var codes = code.Length == 12 ? new[] { code, barcode[^13..] } : [code];
        var foodId = 0;
        foreach (var candidateCode in codes)
        {
            if (!TakeRequestSlot())
            {
                return new("rate_limited");
            }
            using var searchRequest = Request($"foods/search?query={candidateCode}&dataType=Branded&pageSize=25&sortBy=fdcId&sortOrder=desc");
            using var searchResponse = await client.SendAsync(searchRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (FailureStatus(searchResponse) is { } searchFailure)
            {
                return new(searchFailure);
            }
            using var search = await ReadJsonAsync(searchResponse, cancellationToken);
            if (search.RootElement.ValueKind != JsonValueKind.Object
                || !search.RootElement.TryGetProperty("foods", out var foods) || foods.ValueKind != JsonValueKind.Array)
            {
                return new("unavailable");
            }
            foodId = foods.EnumerateArray().Take(25)
                .Where(food => IsMatchingProduct(food, barcode))
                .Select(food => PositiveId(food, "fdcId").GetValueOrDefault()).Where(id => id > 0).DefaultIfEmpty().Max();
            if (foodId > 0)
            {
                break;
            }
        }
        if (foodId == 0)
        {
            return new("not_found");
        }
        if (!TakeRequestSlot())
        {
            return new("rate_limited");
        }
        using var detailRequest = Request($"food/{foodId}");
        using var detailResponse = await client.SendAsync(detailRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (FailureStatus(detailResponse) is { } detailFailure)
        {
            return new(detailFailure);
        }
        using var detail = await ReadJsonAsync(detailResponse, cancellationToken);
        var product = ParseProduct(detail.RootElement, barcode, foodId, clock.GetUtcNow());
        return product is null ? new("unavailable") : new("food_data_central", product);
    }

    private HttpRequestMessage Request(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        // Keeping the key out of URLs prevents it from appearing in ordinary HTTP request logs.
        request.Headers.Add("X-Api-Key", _apiKey);
        return request;
    }

    private bool TakeRequestSlot()
    {
        var now = clock.GetUtcNow();
        while (_requests.TryPeek(out var oldest) && oldest <= now.AddMinutes(-1))
        {
            _requests.Dequeue();
        }
        if (now < _retryAfter || _requests.Count >= 15)
        {
            return false;
        }
        _requests.Enqueue(now);
        return true;
    }

    private string? FailureStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var now = clock.GetUtcNow();
            var retry = response.Headers.RetryAfter;
            _retryAfter = retry?.Date ?? now.Add(retry?.Delta ?? TimeSpan.FromMinutes(1));
            if (_retryAfter <= now)
            {
                _retryAfter = now.AddMinutes(1);
            }
            return "rate_limited";
        }
        return response.StatusCode == HttpStatusCode.NotFound ? "not_found"
            : response.IsSuccessStatusCode ? null : "unavailable";
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await response.Content.LoadIntoBufferAsync(1_048_576, cancellationToken);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken), new JsonDocumentOptions { MaxDepth = 32 });
    }

    internal static ProductDto? ParseProduct(JsonElement product, string barcode, int foodId, DateTimeOffset retrievedAt)
    {
        if (!IsMatchingProduct(product, barcode) || PositiveId(product, "fdcId") != foodId
            || Text(product, "description") is not { } name)
        {
            return null;
        }
        var servingSize = Number(product, "servingSize");
        var servingUnit = Text(product, "servingSizeUnit")?.ToLowerInvariant() switch
        {
            "g" or "grm" => "g",
            "ml" or "mlt" => "ml",
            _ => null
        };
        var hasServingBasis = servingSize is > 0 and <= 100_000 && servingUnit is not null;
        var basis = hasServingBasis ? servingUnit ?? "g" : "g";
        var label = product.TryGetProperty("labelNutrients", out var values) && values.ValueKind == JsonValueKind.Object ? values : default;
        decimal? Nutrient(string labelName, int nutrientId, string nutrientUnit, decimal maximum)
        {
            if (hasServingBasis && label.ValueKind == JsonValueKind.Object && label.TryGetProperty(labelName, out var item)
                && Number(item, "value") is >= 0 and <= 1_000_000_000m and var amount)
            {
                // Multiply only after bounding the source; very small servings can otherwise overflow decimal.
                if (servingSize >= 0.001m)
                {
                    var per100 = amount * 100m / servingSize.Value;
                    if (per100 <= maximum)
                    {
                        return decimal.Round(per100, 3);
                    }
                }
            }
            if (basis != "g" || !product.TryGetProperty("foodNutrients", out var nutrients) || nutrients.ValueKind != JsonValueKind.Array)
            {
                return null;
            }
            foreach (var nutrient in nutrients.EnumerateArray())
            {
                if (nutrient.ValueKind == JsonValueKind.Object && nutrient.TryGetProperty("nutrient", out var metadata)
                    && PositiveId(metadata, "id") == nutrientId && string.Equals(Text(metadata, "unitName"), nutrientUnit, StringComparison.OrdinalIgnoreCase)
                    && Number(nutrient, "amount") is >= 0 and var value && value <= maximum)
                {
                    return decimal.Round(value, 3);
                }
            }
            return null;
        }
        var calories = Nutrient("calories", 1008, "kcal", 1000);
        var protein = Nutrient("protein", 1003, "g", 100);
        var carbs = Nutrient("carbohydrates", 1005, "g", 100);
        var fat = Nutrient("fat", 1004, "g", 100);
        var hasNutrition = calories.HasValue || protein.HasValue || carbs.HasValue || fat.HasValue;
        return new(Guid.Empty, barcode, name, Text(product, "brandName") ?? Text(product, "brandOwner"), null, 1, "pcs", null,
            calories, protein, carbs, fat, hasNutrition ? basis : null, "FoodDataCentral",
            $"https://fdc.nal.usda.gov/food-details/{foodId}/nutrients", retrievedAt, foodId, retrievedAt);
    }

    private static bool IsMatchingProduct(JsonElement product, string barcode) => product.ValueKind == JsonValueKind.Object
        && Text(product, "dataType") == "Branded" && Barcode.TryNormalize(Text(product, "gtinUpc"), out var normalized) && normalized == barcode;

    private static int? PositiveId(JsonElement parent, string name) => parent.ValueKind == JsonValueKind.Object
        && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var id) && id > 0 ? id : null;

    private static string? Text(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) ? null : text[..Math.Min(text.Length, 200)];
    }

    private static decimal? Number(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            return null;
        }
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    public void Dispose()
    {
        _cache.Dispose();
        _gate.Dispose();
    }
}
