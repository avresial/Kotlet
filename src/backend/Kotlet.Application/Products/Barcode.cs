namespace Kotlet.Application.Products;

public static class Barcode
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        var code = value?.Trim();
        if (code is null || code.Length is not (8 or 12 or 13 or 14) || code.Any(character => character is < '0' or > '9'))
        {
            return false;
        }
        var sum = 0;
        for (var index = code.Length - 2; index >= 0; index--)
        {
            sum += (code[index] - '0') * ((code.Length - 2 - index) % 2 == 0 ? 3 : 1);
        }
        if ((10 - sum % 10) % 10 != code[^1] - '0' || code.All(character => character == '0'))
        {
            return false;
        }
        normalized = code.PadLeft(14, '0');
        return true;
    }

    // OFF stores EAN-8 as eight digits, UPC-A as EAN-13, and preserves genuine GTIN-14.
    public static string ToOpenFoodFactsCode(string normalized) =>
        normalized.StartsWith("000000", StringComparison.Ordinal) ? normalized[^8..] :
        normalized.StartsWith('0') ? normalized[1..] : normalized;
}
