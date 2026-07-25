using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kotlet.Bench;

/// <summary>One complete benchmark run, shaped so two runs can be diffed field by field.</summary>
public sealed record BenchResult(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    string Mode,
    string Fixture,
    ToolSurfaceResult ToolSurface,
    IReadOnlyList<CallResult> Calls,
    SessionResult? Session,
    IReadOnlyList<ApiCallResult> ApiCalls)
{
    // v2 added the REST endpoint measurements.
    public const int CurrentSchemaVersion = 2;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static BenchResult FromJson(string json) =>
        JsonSerializer.Deserialize<BenchResult>(json, Json)
        ?? throw new InvalidOperationException("The baseline file is empty or malformed.");
}

/// <summary>
/// The cost of the tool catalogue itself. Hosts send these definitions to the model on every
/// turn, so this is the fixed tax on a conversation before any tool is even called.
/// </summary>
public sealed record ToolSurfaceResult(
    int ToolCount,
    int Bytes,
    int EstimatedTokens,
    IReadOnlyDictionary<string, int> BytesByField,
    IReadOnlyList<ToolCost> Tools);

public sealed record ToolCost(
    string Name,
    int Bytes,
    int DescriptionBytes,
    int InputSchemaBytes,
    int OutputSchemaBytes,
    int MetaBytes);

/// <summary>
/// The cost of one tool call. <see cref="RedundantBytes"/> is the smaller of the text and
/// structuredContent copies: when a tool returns both, one of them is a duplicate the model
/// pays for twice.
/// </summary>
public sealed record CallResult(
    string Label,
    string Tool,
    double MedianMs,
    double MinMs,
    int WireBytes,
    int TextBytes,
    int StructuredBytes,
    int RedundantBytes,
    int? DbQueries);

/// <summary>
/// The cost of one REST call the frontend makes to paint a screen. No duplicate-payload column
/// here: unlike MCP tool results, a REST response is sent once.
/// </summary>
public sealed record ApiCallResult(
    string Label,
    string Screen,
    string Path,
    int StatusCode,
    double MedianMs,
    double MinMs,
    int Bytes,
    int? DbQueries);

/// <summary>
/// An end-to-end agent workflow. Round trips matter most here: each one is a separate model
/// turn, which costs far more wall-clock than any server-side work.
/// </summary>
public sealed record SessionResult(
    string Label,
    int RoundTrips,
    double TotalMs,
    int WireBytes,
    int? DbQueries);
