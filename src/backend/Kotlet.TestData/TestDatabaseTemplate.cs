using Kotlet.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Kotlet.TestData;

/// <summary>
/// Builds the seeded fixture into a SQLite file once per process, then hands out cheap copies.
/// Building costs a few hundred milliseconds; copying costs a file write, so every test class,
/// benchmark run, or dev session can have its own isolated database without paying for seeding
/// again.
/// </summary>
/// <remarks>
/// The template is generated rather than committed. A checked-in database file would freeze the
/// schema at the moment it was built and drift silently the next time a migration lands; going
/// through EF on every build means the fixture and the model can never disagree.
/// </remarks>
public static class TestDatabaseTemplate
{
    private static readonly SemaphoreSlim BuildLock = new(1, 1);
    private static string? templatePath;

    /// <summary>
    /// Where the template and its copies live. Scoped to this process and removed when the
    /// process exits, so parallel runs cannot collide and repeated runs do not accumulate
    /// databases in the temp directory.
    /// </summary>
    private static readonly string WorkingDirectory = CreateWorkingDirectory();

    private static string CreateWorkingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "kotlet-testdata", Environment.ProcessId.ToString());
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: a file still held open is left for the OS to reclaim.
            }
        };
        return directory;
    }

    /// <summary>Builds the template if this process has not built it yet, and returns its path.</summary>
    public static async Task<string> EnsureBuiltAsync(CancellationToken cancellationToken = default)
    {
        if (templatePath is { } existing) return existing;

        await BuildLock.WaitAsync(cancellationToken);
        try
        {
            if (templatePath is { } built) return built;

            Directory.CreateDirectory(WorkingDirectory);
            var path = Path.Combine(WorkingDirectory, "template.db");
            if (File.Exists(path)) File.Delete(path);

            await using (var dbContext = CreateContext(ConnectionStringFor(path)))
            {
                await dbContext.Database.EnsureCreatedAsync(cancellationToken);
                await KotletTestData.SeedAsync(dbContext, cancellationToken);
            }

            // EF pools connections, and SQLite keeps the file locked (and the WAL unmerged)
            // until they are actually closed. Copying before that yields a torn database.
            SqliteConnection.ClearAllPools();
            templatePath = path;
            return path;
        }
        finally
        {
            BuildLock.Release();
        }
    }

    /// <summary>
    /// Returns a private copy of the seeded database. The caller owns the file and should pass
    /// it to <see cref="Delete"/> when finished.
    /// </summary>
    public static async Task<string> CreateCopyAsync(CancellationToken cancellationToken = default)
    {
        var template = await EnsureBuiltAsync(cancellationToken);
        var copy = Path.Combine(WorkingDirectory, $"kotlet-{Guid.NewGuid():N}.db");
        File.Copy(template, copy, overwrite: true);
        return copy;
    }

    /// <summary>A private seeded database, expressed as a connection string.</summary>
    public static async Task<string> CreateCopyConnectionStringAsync(CancellationToken cancellationToken = default) =>
        ConnectionStringFor(await CreateCopyAsync(cancellationToken));

    public static string ConnectionStringFor(string path) =>
        new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();

    public static void Delete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // A copy left behind in the process temp directory is harmless; the OS reclaims it.
        }
    }

    private static KotletDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<KotletDbContext>()
            .UseSqlite(connectionString)
            .UseOpenIddict()
            .Options);
}
