using Kotlet.TestData;

namespace Kotlet.McpBench;

/// <summary>
/// Entry point. Deliberately a named class rather than top-level statements: those would
/// generate a second <c>Program</c> type in the global namespace and shadow the API's own
/// entry point, which <see cref="InProcessTarget"/> needs to boot the real application.
/// </summary>
public static class BenchProgram
{
    private const string ProtocolVersion = "2025-11-25";

    public static async Task<int> Main(string[] args)
    {
        BenchOptions options;
        try
        {
            options = BenchOptions.Parse(args);
        }
        catch (HelpRequested)
        {
            Console.WriteLine(BenchOptions.Help);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }

        await using var target = await BenchTarget.CreateAsync(options);
        var counter = target.QueryCounter;

        // In-process runs sign in as the shared fixture's owner; the household and its data are
        // already there, so nothing has to be created before measuring.
        var credentials = options.Url is null
            ? new Credentials(KotletTestData.Owner.Email, KotletTestData.Owner.Password)
            : new Credentials(options.Email!, options.Password!);

        var session = await McpSession.ConnectAsync(
            target.CreateClient(), credentials, target.Resource, target.ClientId, ProtocolVersion);

        // The first request pays for JIT, EF model building and connection setup. Excluding it
        // keeps the reported medians about the server's steady state rather than its startup.
        await session.SendAsync("tools/list", new { });

        var toolsList = await session.SendAsync("tools/list", new { });
        var surface = ToolSurface.Analyze(toolsList);

        var scenario = new Scenario(session, counter);
        if (!options.SeedsData)
            Console.Error.WriteLine(
                "Read-only run: payload sizes reflect whatever this household already holds, " +
                "so compare remote runs against each other rather than against an in-process baseline.");

        var calls = await MeasureCallsAsync(scenario, session, counter, options.Runs);

        // The REST surface the frontend uses, on its own authenticated client so the MCP
        // handshake's tokens and cookies cannot influence it.
        var apiClient = target.CreateClient();
        await McpSession.SignInAsync(apiClient, credentials);
        var apiCalls = await MeasureApiCallsAsync(new ApiScenario(apiClient, counter), options.Runs);

        // Every read is measured before this point. The import workflow creates a recipe stamped
        // with the current time, which would otherwise leak a variable-length timestamp into any
        // recipe list measured afterwards. It also only runs where writing is safe.
        var agentSession = options.SeedsData ? await scenario.MeasureRecipeImportAsync() : null;

        var result = new BenchResult(
            BenchResult.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            target.Mode,
            options.SeedsData ? Scenario.FixtureDescription : "existing household data (read-only run)",
            surface,
            calls,
            agentSession,
            apiCalls);

        if (options.JsonPath is { } jsonPath)
        {
            var fullPath = Path.GetFullPath(jsonPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, result.ToJson());
        }

        // --stdout-json only changes what lands on stdout. Saving a baseline and gating on
        // regressions still happen, so a CI job can emit machine-readable output and fail.
        var baseline = await LoadBaselineAsync(options);
        Console.WriteLine(options.JsonToStdout
            ? result.ToJson()
            : Report.Render(result, baseline, options.TopTools));

        if (options.Save)
        {
            var path = Path.GetFullPath(options.BaselinePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, result.ToJson());
            if (!options.JsonToStdout)
                Console.WriteLine($"Baseline written to {path}");
        }

        return Verdict(result, baseline, options);
    }

    private static async Task<List<CallResult>> MeasureCallsAsync(
        Scenario scenario, McpSession session, DbQueryCounter? counter, int runs)
    {
        var calls = new List<CallResult>();
        foreach (var (label, tool, arguments) in scenario.ReadCalls())
        {
            var samples = new List<McpCallResult>(runs);
            for (var run = 0; run < runs; run++)
                samples.Add(await session.CallToolAsync(tool, arguments, counter));

            var last = samples[^1];
            var (text, structured) = last.ContentSplit();
            var timings = samples.Select(sample => sample.ElapsedMs).Order().ToArray();
            calls.Add(new CallResult(
                label,
                tool,
                Median(timings),
                timings[0],
                last.WireBytes,
                text,
                structured,
                // Both copies carry the same data, so the smaller one is what the model pays twice for.
                text > 0 && structured > 0 ? Math.Min(text, structured) : 0,
                last.DbQueries));
        }

        return calls;
    }

    private static async Task<List<ApiCallResult>> MeasureApiCallsAsync(ApiScenario scenario, int runs)
    {
        var calls = new List<ApiCallResult>();
        foreach (var (label, screen, path) in ApiScenario.Calls())
        {
            var samples = new List<(int StatusCode, double ElapsedMs, int Bytes, int? Queries)>(runs);
            for (var run = 0; run < runs; run++)
                samples.Add(await scenario.GetAsync(path));

            var last = samples[^1];
            var timings = samples.Select(sample => sample.ElapsedMs).Order().ToArray();
            calls.Add(new ApiCallResult(
                label, screen, path, last.StatusCode,
                Median(timings), timings[0], last.Bytes, last.Queries));
        }

        return calls;
    }

    private static int Verdict(BenchResult result, BenchResult? baseline, BenchOptions options)
    {
        if (options.FailOnRegressionPercent is not { } threshold || baseline is null)
            return 0;

        var regressions = Report.Headlines(result, baseline)
            .Where(entry => entry.Baseline > 0
                            && 100.0 * (entry.Current - entry.Baseline) / entry.Baseline > threshold)
            .ToArray();
        if (regressions.Length == 0)
            return 0;

        Console.Error.WriteLine($"Regressed by more than {threshold}%:");
        foreach (var (label, current, before) in regressions)
            Console.Error.WriteLine($"  {label}: {before:N0} -> {current:N0}");
        return 1;
    }

    private static double Median(double[] sorted) =>
        sorted.Length % 2 == 1
            ? sorted[sorted.Length / 2]
            : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;

    private static async Task<BenchResult?> LoadBaselineAsync(BenchOptions options)
    {
        if (options.NoCompare) return null;
        var path = Path.GetFullPath(options.BaselinePath);
        if (!File.Exists(path)) return null;
        try
        {
            var baseline = BenchResult.FromJson(await File.ReadAllTextAsync(path));
            if (baseline.SchemaVersion != BenchResult.CurrentSchemaVersion)
            {
                Console.Error.WriteLine(
                    $"Baseline uses schema v{baseline.SchemaVersion}, this build writes " +
                    $"v{BenchResult.CurrentSchemaVersion}. Re-record it with --save.");
                return null;
            }
            return baseline;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Could not read the baseline at {path}: {exception.Message}");
            return null;
        }
    }
}
