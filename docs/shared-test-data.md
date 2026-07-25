# The shared test dataset

`src/backend/Kotlet.TestData` holds one dataset used by the MCP benchmark, integration tests,
browser tests, and local development. One fixture means a number measured in the benchmark
describes the same household a test asserts on and a developer sees in the browser.

## What is in it

| | |
| --- | --- |
| Household | `Test kitchen` (the default house id) |
| Accounts | `testuser@kotlet.local` / `TestUser123!`, `admin@kotlet.local` / `Admin123!`, `housemate@kotlet.local` / `Housemate123!` — all three are members |
| Ingredients | the full production seed catalogue from `SeedData/ingredients.csv` |
| Recipes | 12, six ingredients each, a quarter marked AI-assisted with a source URL |
| Meal plan | 14 days from 2026-03-02, three slots a day, participants and portions filled in |
| Shopping list | 12 items, some already purchased |
| Pantry | 15 items across all three storage locations |
| Prepared meals | 3 |

Everything is deterministic. Identifiers come from `TestIds`, which hashes a name into a GUID,
so `TestIds.Recipe(KotletTestData.RecipeTitle(0))` is the same value on every machine and every
run. Timestamps derive from `KotletTestData.Anchor`. That is what lets the benchmark compare
payload sizes byte for byte and lets a test assert on an id it did not have to create first.

## How it is delivered

It is **generated, not committed**. `TestDatabaseTemplate` runs `EnsureCreated` plus the seeder
into a SQLite file once per process, then hands out file copies:

```csharp
var connectionString = await TestDatabaseTemplate.CreateCopyConnectionStringAsync();
```

Building costs a few hundred milliseconds; each copy costs a file write. Every consumer gets an
isolated database without paying for seeding again.

A checked-in `.db` would have been simpler to start with and worse to live with: it freezes the
schema at the moment it was built, drifts silently the first time a migration lands, produces
unreviewable binary diffs, and cannot be merged. Going through EF on every build means the
fixture and the model can never disagree — and if the seeder ever falls behind the model,
`SharedTestDataTests` fails immediately and in one place.

## Using it

**Integration tests** — derive from `SeededWebApplicationFactory` instead of
`TestWebApplicationFactory`:

```csharp
public sealed class MyTests(SeededWebApplicationFactory factory)
    : IClassFixture<SeededWebApplicationFactory>
```

Each factory instance takes its own copy, so classes stay isolated. `TestWebApplicationFactory`
still exists and still starts empty; use it when a test creates exactly what it asserts on and
would be confused by pre-existing rows.

**Local development** — set `Database__SeedSampleData=true` alongside `Database__Provider=Sqlite`.
The `kotlet-runtime` scripts already do this, so `run.sh` gives you a populated household.

**Benchmark** — automatic. See [performance.md](performance.md).

## Changing it

Adding rows changes benchmark numbers, so treat the fixture as part of the measurement
contract: change it and re-record the benchmark baseline in the same commit
(`dotnet run --project tools/Kotlet.Bench -- --save`).

Prefer growing the fixture over inventing local data in a test. A test that seeds its own
household is fine when it is testing creation; a test that seeds a household in order to read
from it is duplicating what is already here.
