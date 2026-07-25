using System.Globalization;
using System.Text;

namespace Kotlet.McpBench;

/// <summary>Renders a run as a console report, and a run against a baseline as a diff.</summary>
public static class Report
{
    public static string Render(BenchResult result, BenchResult? baseline, int topTools)
    {
        var text = new StringBuilder();
        text.AppendLine();
        text.AppendLine($"Kotlet MCP benchmark  ({result.Mode})");
        text.AppendLine($"  captured  {result.CapturedAtUtc:u}");
        text.AppendLine($"  fixture   {result.Fixture}");
        if (baseline is not null)
            text.AppendLine($"  baseline  {baseline.CapturedAtUtc:u} ({baseline.Mode})");
        text.AppendLine();

        RenderToolSurface(text, result, baseline, topTools);
        RenderCalls(text, result, baseline);
        RenderSession(text, result, baseline);
        RenderHeadline(text, result, baseline);
        return text.ToString();
    }

    private static void RenderToolSurface(StringBuilder text, BenchResult result, BenchResult? baseline, int topTools)
    {
        var surface = result.ToolSurface;
        var before = baseline?.ToolSurface;

        text.AppendLine("TOOL SURFACE  (sent to the model on every turn)");
        text.AppendLine($"  {"tools",-16}{Cell(surface.ToolCount, before?.ToolCount)}");
        text.AppendLine($"  {"bytes",-16}{Cell(surface.Bytes, before?.Bytes)}");
        text.AppendLine($"  {"est. tokens",-16}{Cell(surface.EstimatedTokens, before?.EstimatedTokens)}");
        text.AppendLine();
        text.AppendLine($"  {"by field",-16}{"bytes",12}{"share",8}   {"vs baseline",-24}");
        foreach (var (field, bytes) in surface.BytesByField)
        {
            var share = surface.Bytes == 0 ? 0 : 100.0 * bytes / surface.Bytes;
            var baselineBytes = before?.BytesByField.GetValueOrDefault(field);
            text.AppendLine(
                $"  {field,-16}{bytes,12:N0}{share,7:F1}%   {Delta(bytes, baselineBytes),-24}");
        }

        text.AppendLine();
        text.AppendLine($"  heaviest tools (top {topTools})");
        text.AppendLine($"  {"tool",-32}{"bytes",8}{"desc",8}{"input",8}{"output",8}{"_meta",8}   {"vs baseline",-20}");
        foreach (var tool in surface.Tools.Take(topTools))
        {
            var baselineTool = before?.Tools.FirstOrDefault(candidate => candidate.Name == tool.Name);
            text.AppendLine(
                $"  {tool.Name,-32}{tool.Bytes,8:N0}{tool.DescriptionBytes,8:N0}{tool.InputSchemaBytes,8:N0}" +
                $"{tool.OutputSchemaBytes,8:N0}{tool.MetaBytes,8:N0}   {Delta(tool.Bytes, baselineTool?.Bytes),-20}");
        }

        if (before is not null)
        {
            var added = surface.Tools.Select(tool => tool.Name).Except(before.Tools.Select(tool => tool.Name)).ToArray();
            var removed = before.Tools.Select(tool => tool.Name).Except(surface.Tools.Select(tool => tool.Name)).ToArray();
            if (added.Length > 0) text.AppendLine($"  + added:   {string.Join(", ", added)}");
            if (removed.Length > 0) text.AppendLine($"  - removed: {string.Join(", ", removed)}");
        }

        text.AppendLine();
    }

    private static void RenderCalls(StringBuilder text, BenchResult result, BenchResult? baseline)
    {
        text.AppendLine("TOOL CALLS  (median of repeats; 'dup' is the copy sent twice as text + structuredContent)");
        text.AppendLine(
            $"  {"call",-30}{"ms",8}{"wire",9}{"dup",8}{"sql",6}   {"wire vs baseline",-24}");
        foreach (var call in result.Calls)
        {
            var before = baseline?.Calls.FirstOrDefault(candidate => candidate.Label == call.Label);
            var queries = call.DbQueries?.ToString(CultureInfo.InvariantCulture) ?? "-";
            text.AppendLine(
                $"  {call.Label,-30}{call.MedianMs,8:F1}{call.WireBytes,9:N0}{call.RedundantBytes,8:N0}{queries,6}" +
                $"   {Delta(call.WireBytes, before?.WireBytes),-24}");
        }

        var totalWire = result.Calls.Sum(call => call.WireBytes);
        var totalRedundant = result.Calls.Sum(call => call.RedundantBytes);
        var totalQueries = result.Calls.Sum(call => call.DbQueries ?? 0);
        var baselineWire = baseline?.Calls.Sum(call => call.WireBytes);
        text.AppendLine(
            $"  {"TOTAL",-30}{"",8}{totalWire,9:N0}{totalRedundant,8:N0}{totalQueries,6}   {Delta(totalWire, baselineWire),-24}");
        text.AppendLine();

        if (baseline is not null)
        {
            var missing = baseline.Calls.Select(call => call.Label).Except(result.Calls.Select(call => call.Label)).ToArray();
            if (missing.Length > 0)
                text.AppendLine($"  ! not measured this run: {string.Join(", ", missing)}");
        }
    }

    private static void RenderSession(StringBuilder text, BenchResult result, BenchResult? baseline)
    {
        if (result.Session is not { } session) return;
        var before = baseline?.Session;

        text.AppendLine($"AGENT SESSION  \"{session.Label}\"");
        text.AppendLine($"  {"round trips",-16}{Cell(session.RoundTrips, before?.RoundTrips)}   (each one is a model turn)");
        text.AppendLine($"  {"wire bytes",-16}{Cell(session.WireBytes, before?.WireBytes)}");
        if (session.DbQueries is { } queries)
            text.AppendLine($"  {"sql queries",-16}{Cell(queries, before?.DbQueries)}");
        text.AppendLine($"  {"server ms",-16}{session.TotalMs,12:F1}");
        text.AppendLine();
    }

    private static void RenderHeadline(StringBuilder text, BenchResult result, BenchResult? baseline)
    {
        if (baseline is null)
        {
            text.AppendLine("No baseline compared. Use --save to record this run as the baseline.");
            return;
        }

        text.AppendLine("HEADLINE");
        foreach (var (label, current, before) in Headlines(result, baseline))
            text.AppendLine($"  {label,-28}{Delta(current, before)}");
        text.AppendLine();
    }

    /// <summary>The numbers worth gating on: tool-surface size, payload size, and query count.</summary>
    public static IEnumerable<(string Label, int Current, int Baseline)> Headlines(
        BenchResult result, BenchResult baseline) =>
    [
        ("tool surface bytes", result.ToolSurface.Bytes, baseline.ToolSurface.Bytes),
        ("tool call wire bytes", result.Calls.Sum(call => call.WireBytes), baseline.Calls.Sum(call => call.WireBytes)),
        ("duplicated bytes", result.Calls.Sum(call => call.RedundantBytes), baseline.Calls.Sum(call => call.RedundantBytes)),
        ("tool call sql queries", result.Calls.Sum(call => call.DbQueries ?? 0), baseline.Calls.Sum(call => call.DbQueries ?? 0)),
        ("session round trips", result.Session?.RoundTrips ?? 0, baseline.Session?.RoundTrips ?? 0)
    ];

    private static string Cell(int current, int? baseline) =>
        $"{current,12:N0}   {Delta(current, baseline)}";

    private static string Delta(int current, int? baseline)
    {
        if (baseline is not { } before) return string.Empty;
        var change = current - before;
        if (change == 0) return $"= {before:N0}";
        var percent = before == 0 ? double.PositiveInfinity : 100.0 * change / before;
        var verdict = change < 0 ? "improved" : "REGRESSED";
        return $"{before:N0} -> {current:N0}  {(change < 0 ? "" : "+")}{change:N0} ({percent:+0.0;-0.0}%) {verdict}";
    }
}
