using System.Globalization;
using System.Net;
using System.Text.Json;
using Kotlet.Application.Products;
using Microsoft.Extensions.Caching.Memory;

namespace Kotlet.Infrastructure.Products;

// One process-wide client shares both the bounded cache and the outbound request budget.
internal sealed class OpenFoodFactsClient(IHttpClientFactory httpClients, TimeProvider clock) : IOpenFoodFactsClient, IDisposable
{
    internal const string HttpClientName = "OpenFoodFacts";
    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = 512 });
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _requests = new();
    private DateTimeOffset _retryAfter;
    private const string Fields = "code,product_name,product_name_pl,brands,quantity,product_quantity,product_quantity_unit,nutriments,nutrition_data_per";

    public async Task<ProductLookupResult> LookupAsync(string normalizedBarcode, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(normalizedBarcode, out ProductLookupResult? cached))
        {
            return cached!;
        }
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(normalizedBarcode, out cached))
            {
                return cached!;
            }
            var now = clock.GetUtcNow();
            while (_requests.TryPeek(out var oldest) && oldest <= now.AddMinutes(-1))
            {
                _requests.Dequeue();
            }
            if (now < _retryAfter || _requests.Count >= 15)
            {
                return new("rate_limited");
            }
            _requests.Enqueue(now);
            ProductLookupResult result;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var requestToken = timeout.Token;
                var code = Barcode.ToOpenFoodFactsCode(normalizedBarcode);
                using var response = await httpClients.CreateClient(HttpClientName).GetAsync(
                    $"api/v3/product/{code}?fields={Fields}", HttpCompletionOption.ResponseHeadersRead, requestToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retry = response.Headers.RetryAfter;
                    _retryAfter = retry?.Date ?? now.Add(retry?.Delta ?? TimeSpan.FromMinutes(1));
                    if (_retryAfter <= now)
                    {
                        _retryAfter = now.AddMinutes(1);
                    }
                    return new("rate_limited");
                }
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    result = new("not_found");
                }
                else if (!response.IsSuccessStatusCode)
                {
                    result = new("unavailable");
                }
                else
                {
                    // Even selected fields are untrusted. Bound payload size, nesting and types.
                    await response.Content.LoadIntoBufferAsync(1_048_576, requestToken);
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(requestToken), new JsonDocumentOptions { MaxDepth = 32 });
                    result = Parse(document.RootElement, normalizedBarcode, now);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = new("unavailable");
            }
            catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException or IOException)
            {
                result = new("unavailable");
            }
            var lifetime = result.Status switch
            {
                "open_food_facts" => TimeSpan.FromHours(24),
                "not_found" => TimeSpan.FromMinutes(15),
                _ => TimeSpan.FromSeconds(30)
            };
            _cache.Set(normalizedBarcode, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = lifetime, Size = 1 });
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    internal static ProductLookupResult Parse(JsonElement root, string barcode, DateTimeOffset retrievedAt)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new("unavailable");
        }
        var status = Text(root, "status");
        if (status is not ("success" or "success_with_warnings" or "success_with_errors")
            || !root.TryGetProperty("product", out var product) || product.ValueKind != JsonValueKind.Object)
        {
            return new("unavailable");
        }
        if (!Barcode.TryNormalize(Text(product, "code"), out var returnedCode) || returnedCode != barcode)
        {
            return new("unavailable");
        }
        var quantity = Number(product, "product_quantity");
        var packageUnit = Text(product, "product_quantity_unit");
        var unit = packageUnit switch { "g" or "kg" => "g", "ml" or "cl" or "l" => "ml", _ => "pcs" };
        var basis = unit is "g" or "ml" ? unit : null;
        var multiplier = packageUnit switch { "kg" or "l" => 1000m, "cl" => 10m, _ => 1m };
        if (quantity is <= 0 or > 99999 || quantity is null || unit == "pcs")
        {
            quantity = 1;
            unit = "pcs";
            multiplier = 1;
        }
        var packageQuantity = decimal.Round(quantity.Value * multiplier, 3);
        if (packageQuantity <= 0)
        {
            packageQuantity = 1;
            unit = "pcs";
        }
        // OFF's *_100g fields represent 100 g / 100 ml, not per-serving values.
        // Do not guess the nutrition basis for an unknown package unit.
        var nutrients = product.TryGetProperty("nutriments", out var values) && values.ValueKind == JsonValueKind.Object ? values : default;
        return new("open_food_facts", new ProductDto(
            Guid.Empty, barcode, Text(product, "product_name_pl") ?? Text(product, "product_name") ?? string.Empty,
            Text(product, "brands"), Text(product, "quantity"), packageQuantity, unit, null,
            basis is null ? null : Nutrient(nutrients, "energy-kcal_100g", 1000),
            basis is null ? null : Nutrient(nutrients, "proteins_100g", 100),
            basis is null ? null : Nutrient(nutrients, "carbohydrates_100g", 100),
            basis is null ? null : Nutrient(nutrients, "fat_100g", 100), basis, "OpenFoodFacts",
            $"https://world.openfoodfacts.org/product/{Barcode.ToOpenFoodFactsCode(barcode)}", retrievedAt));
    }

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

    private static decimal? Nutrient(JsonElement parent, string name, decimal maximum)
    {
        var number = Number(parent, name);
        return number >= 0 && number <= maximum ? decimal.Round(number.Value, 3) : null;
    }

    public void Dispose()
    {
        _cache.Dispose();
        _gate.Dispose();
    }
}
