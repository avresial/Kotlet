# Measuring MCP performance

When an AI agent feels slow talking to Kotlet, the wall-clock of a single HTTP request is
rarely the reason. Three other things dominate, and all three are measurable:

1. **The tool catalogue.** Hosts send every tool definition to the model on *every* turn. A
   fat catalogue is a fixed tax paid before the user's question is even read.
2. **Payload size.** Tool results become context. A result sent twice costs twice.
3. **Round trips.** Each tool call is a separate model turn — seconds, not milliseconds. A
   workflow that needs five calls is slower than any server-side optimisation can fix.

`tools/Kotlet.McpBench` measures all three and diffs a run against a recorded baseline.

## Running it

```bash
dotnet run --project tools/Kotlet.McpBench
```

That boots the real API in-process against a private copy of the shared test fixture
(`Kotlet.TestData`), replays a fixed set of MCP calls, and prints a report compared against
`tools/Kotlet.McpBench/baseline.json`. Nothing external is required — no Docker, no
PostgreSQL, no deployed instance.

The fixture matters: it carries the **full production ingredient catalogue**, twelve recipes,
a fourteen-day meal plan, a shopping list, and a pantry. Measuring against a handful of
invented rows understates exactly the calls that hurt — `get_ingredients` runs a fuzzy match
across the whole catalogue, so its cost is invisible on a near-empty database.

```bash
# record the current numbers as the baseline (do this once a change is accepted)
dotnet run --project tools/Kotlet.McpBench -- --save

# fail the build when a headline metric grows by more than 1%
dotnet run --project tools/Kotlet.McpBench -- --fail-on-regression 1

# machine-readable output for a script or CI artifact
dotnet run --project tools/Kotlet.McpBench -- --json out/mcp-bench.json
```

`--help` lists every option.

## Reading the report

```text
TOOL SURFACE  (sent to the model on every turn)
  tools                     36
  bytes                 72,399
  est. tokens           18,099

  by field               bytes   share
  outputSchema          35,911   49.6%
  inputSchema           14,874   20.5%
  _meta                  7,204   10.0%
  description            6,563    9.1%
```

The `by field` breakdown is the useful part: it says *which* part of the tool definitions is
expensive. Descriptions are documentation an agent actually reads; `outputSchema` and `_meta`
are plumbing that costs the same tokens.

```text
TOOL CALLS  (median of repeats; 'dup' is the copy sent twice as text + structuredContent)
  call                                ms     wire     dup   sql
  get_meal_plan (7d)                33.2   15,480   5,832    35
```

- `wire` — bytes the server sent back for that call.
- `dup` — how much of `wire` is the same data serialized twice, once as a text block and once
  as `structuredContent`. Tools declaring `UseStructuredContent = true` return both.
- `sql` — database commands the call issued, counted from EF Core's `DiagnosticSource`. This is
  the number that exposes N+1 loops: a call whose `sql` grows with the requested range is
  querying per item.

```text
AGENT SESSION  "Import a recipe found online"
  round trips                5      (each one is a model turn)
```

Round trips are the most expensive unit in the report. Removing one is worth more than any
byte-level saving.

## What is trustworthy, and what is not

**Trust the byte counts and the SQL counts.** They are deterministic: two runs on the same
commit produce byte-for-byte identical numbers, because the fixture fixes every identifier and
every timestamp. A change in those columns is a real change, not noise.

**Do not read the `ms` column as production latency.** It is in-process against a private
file-backed SQLite copy: no network, no connection pool, no real query planner. It is useful
for comparing a call against itself across commits, and useless as an absolute number. A call
issuing 35 queries costs tens of milliseconds here and considerably more against a managed
PostgreSQL instance several milliseconds away.

For real wall-clock, point the benchmark at a deployment:

```bash
dotnet run --project tools/Kotlet.McpBench -- \
  --url https://your-instance.example.com \
  --email you@example.com --password '...'
```

A remote run is read-only by default — it will not seed fixture data into a real household.
That also means its payload sizes describe *that* household, so remote runs are comparable to
each other, not to in-process baselines. SQL counts are unavailable remotely. Pass
`--allow-writes` only against a throwaway account.

## Keeping the baseline honest

The baseline is only meaningful while the fixture and the call list stay fixed. The call list
lives in `tools/Kotlet.McpBench/Scenario.cs`; the data lives in
`src/backend/Kotlet.TestData/KotletTestData.cs` and is shared with the integration tests and
local development. Changing either invalidates every earlier baseline — when you do, re-record
with `--save` in the same commit and say so in the message, otherwise the next diff will report
a change that never happened.

Adding or removing a tool legitimately changes the tool surface. The report lists added and
removed tool names so that kind of movement is explained rather than mysterious.
