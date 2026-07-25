using System.Text.Json;

namespace Kotlet.McpBench;

/// <summary>Breaks a tools/list response down into what each part of each tool definition costs.</summary>
public static class ToolSurface
{
    /// <summary>
    /// Rough bytes-per-token for JSON tool schemas. Only used to put the byte counts on a
    /// scale that is easy to compare against a model's context budget; deltas are what matter.
    /// </summary>
    private const int BytesPerToken = 4;

    private static readonly string[] Fields =
        ["name", "title", "description", "inputSchema", "outputSchema", "annotations", "_meta"];

    public static ToolSurfaceResult Analyze(McpCallResult response)
    {
        var tools = response.Result.GetProperty("tools");
        var byField = new Dictionary<string, int>();
        var costs = new List<ToolCost>();

        foreach (var tool in tools.EnumerateArray())
        {
            var total = 0;
            var perField = new Dictionary<string, int>();
            foreach (var property in tool.EnumerateObject())
            {
                var size = Minified(property.Value);
                total += size;
                var bucket = Fields.Contains(property.Name) ? property.Name : "other";
                byField[bucket] = byField.GetValueOrDefault(bucket) + size;
                perField[property.Name] = size;
            }

            costs.Add(new ToolCost(
                tool.GetProperty("name").GetString() ?? "(unnamed)",
                total,
                perField.GetValueOrDefault("description"),
                perField.GetValueOrDefault("inputSchema"),
                perField.GetValueOrDefault("outputSchema"),
                perField.GetValueOrDefault("_meta")));
        }

        return new ToolSurfaceResult(
            costs.Count,
            response.WireBytes,
            response.WireBytes / BytesPerToken,
            byField.OrderByDescending(entry => entry.Value).ToDictionary(),
            [.. costs.OrderByDescending(cost => cost.Bytes)]);
    }

    /// <summary>Measures JSON without the transport's whitespace, so formatting never skews a diff.</summary>
    private static int Minified(JsonElement element)
    {
        var buffer = new System.Buffers.ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
            element.WriteTo(writer);
        return buffer.WrittenCount;
    }
}
