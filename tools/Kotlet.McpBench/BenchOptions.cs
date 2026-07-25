namespace Kotlet.McpBench;

/// <summary>Command-line configuration for one benchmark run.</summary>
public sealed record BenchOptions
{
    /// <summary>A deployed instance to measure instead of booting the API in this process.</summary>
    public Uri? Url { get; init; }

    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? ClientId { get; init; }

    /// <summary>Repeats per read-only call; the report takes the median to damp scheduler noise.</summary>
    public int Runs { get; init; } = 5;

    public string BaselinePath { get; init; } = DefaultBaselinePath();
    public bool Save { get; init; }
    public bool NoCompare { get; init; }

    /// <summary>Writes the machine-readable result here as well as to stdout.</summary>
    public string? JsonPath { get; init; }
    public bool JsonToStdout { get; init; }

    public int TopTools { get; init; } = 12;

    /// <summary>
    /// Seeding writes real data, so a remote run stays read-only unless this is set. An
    /// in-process run always seeds: its database is created empty and thrown away.
    /// </summary>
    public bool AllowWrites { get; init; }

    /// <summary>Exit non-zero when a headline metric grows by more than this percentage.</summary>
    public double? FailOnRegressionPercent { get; init; }

    public bool SeedsData => Url is null || AllowWrites;

    public static string DefaultBaselinePath() =>
        Path.Combine(RepoPaths.BenchProject, "baseline.json");

    public static BenchOptions Parse(string[] args)
    {
        var options = new BenchOptions();
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            string Next(string name) => index + 1 < args.Length
                ? args[++index]
                : throw new ArgumentException($"{name} needs a value.");

            options = argument switch
            {
                "--url" => options with { Url = new Uri(Next(argument), UriKind.Absolute) },
                "--email" => options with { Email = Next(argument) },
                "--password" => options with { Password = Next(argument) },
                "--client-id" => options with { ClientId = Next(argument) },
                "--runs" => options with { Runs = int.Parse(Next(argument)) },
                "--baseline" => options with { BaselinePath = Next(argument) },
                "--save" => options with { Save = true },
                "--no-compare" => options with { NoCompare = true },
                "--json" => options with { JsonPath = Next(argument) },
                "--stdout-json" => options with { JsonToStdout = true },
                "--top" => options with { TopTools = int.Parse(Next(argument)) },
                "--allow-writes" => options with { AllowWrites = true },
                "--fail-on-regression" => options with { FailOnRegressionPercent = double.Parse(Next(argument)) },
                "--help" or "-h" => throw new HelpRequested(),
                _ => throw new ArgumentException($"Unknown argument '{argument}'. Try --help.")
            };
        }

        if (options.Url is not null && (options.Email is null || options.Password is null))
            throw new ArgumentException("--url needs --email and --password to obtain an MCP token.");
        if (options.Runs < 1)
            throw new ArgumentException("--runs must be at least 1.");
        return options;
    }

    public const string Help = """
        Kotlet MCP benchmark - measures what an AI agent pays to talk to this server.

        Usage:
          dotnet run --project tools/Kotlet.McpBench [options]

        By default the API is booted in this process against a private copy of the shared test
        fixture (a file-backed SQLite database). That makes byte counts and SQL query counts
        reproducible, which is what you want when checking whether a change helped.

        Options:
          --save                     record this run as the baseline (tools/Kotlet.McpBench/baseline.json)
          --baseline <path>          compare against a different baseline file
          --no-compare               skip the baseline comparison
          --runs <n>                 repeats per read-only call, median reported (default 5)
          --json <path>              also write the machine-readable result to a file
          --stdout-json              print JSON instead of the human report; --save and
                                     --fail-on-regression still apply
          --top <n>                  how many heaviest tools to list (default 12)
          --fail-on-regression <pct> exit 1 when a headline metric grows more than pct percent
          --url <base-url>           measure a deployed instance instead (real network + database)
          --email / --password       account used for the deployed instance
          --client-id <id>           OAuth client id the deployment expects
          --allow-writes             let a deployed run seed fixture data (it writes to that household)

        Examples:
          dotnet run --project tools/Kotlet.McpBench
          dotnet run --project tools/Kotlet.McpBench -- --save
          dotnet run --project tools/Kotlet.McpBench -- --fail-on-regression 2
          dotnet run --project tools/Kotlet.McpBench -- --url https://kotlet.example.com \
            --email me@example.com --password '...'
        """;
}

public sealed class HelpRequested : Exception;
