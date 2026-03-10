using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using InstallerService.Database;

namespace InstallerService.Tests.Database;

public class SqliteMigratorTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _migrationsDir;
    private readonly SqliteMigrator _sut;

    public SqliteMigratorTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"installer_test_{Guid.NewGuid():N}.db");
        _migrationsDir = Path.Combine(Path.GetTempPath(), $"migrations_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_migrationsDir);

        var logger = NullLoggerFactory.Instance.CreateLogger<SqliteMigrator>();
        _sut = new SqliteMigrator($"Data Source={_dbPath}", _migrationsDir, logger);
    }

    public void Dispose()
    {
        try { File.Delete(_dbPath); } catch { }
        try { Directory.Delete(_migrationsDir, true); } catch { }
    }

    [Fact]
    public async Task MigrateAsync_NoScripts_ReturnsEmpty()
    {
        var results = await _sut.MigrateAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task MigrateAsync_SingleScript_AppliesSuccessfully()
    {
        WriteMigration("V001__Create_table.sql",
            "CREATE TABLE test_table (id INTEGER PRIMARY KEY, name TEXT);");

        var results = await _sut.MigrateAsync();
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.Equal("V001", results[0].Version);
        Assert.Equal("Create table", results[0].Description);
    }

    [Fact]
    public async Task MigrateAsync_MultipleScripts_AppliedInOrder()
    {
        WriteMigration("V001__First.sql", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);");
        WriteMigration("V002__Second.sql", "CREATE TABLE t2 (id INTEGER PRIMARY KEY);");
        WriteMigration("V003__Third.sql", "CREATE TABLE t3 (id INTEGER PRIMARY KEY);");

        var results = await _sut.MigrateAsync();
        Assert.Equal(3, results.Count);
        Assert.Equal("V001", results[0].Version);
        Assert.Equal("V002", results[1].Version);
        Assert.Equal("V003", results[2].Version);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task MigrateAsync_AlreadyApplied_Skipped()
    {
        WriteMigration("V001__Init.sql", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);");

        await _sut.MigrateAsync();
        var secondRun = await _sut.MigrateAsync();
        Assert.Empty(secondRun);
    }

    [Fact]
    public async Task MigrateAsync_InvalidSql_ThrowsMigrationException()
    {
        WriteMigration("V001__Bad.sql", "INVALID SQL STATEMENT;");
        await Assert.ThrowsAsync<MigrationException>(() => _sut.MigrateAsync());
    }

    [Fact]
    public async Task GetCurrentVersionAsync_AfterMigration_ReturnsLatest()
    {
        WriteMigration("V001__First.sql", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);");
        WriteMigration("V002__Second.sql", "CREATE TABLE t2 (id INTEGER PRIMARY KEY);");

        await _sut.MigrateAsync();
        var version = await _sut.GetCurrentVersionAsync();
        Assert.Equal("V002", version);
    }

    [Fact]
    public async Task GetCurrentVersionAsync_NoMigrations_ReturnsNull()
    {
        var version = await _sut.GetCurrentVersionAsync();
        Assert.Null(version);
    }

    [Fact]
    public async Task GetAppliedMigrationsAsync_ReturnsAll()
    {
        WriteMigration("V001__A.sql", "CREATE TABLE a (id INTEGER PRIMARY KEY);");
        WriteMigration("V002__B.sql", "CREATE TABLE b (id INTEGER PRIMARY KEY);");

        await _sut.MigrateAsync();
        var applied = await _sut.GetAppliedMigrationsAsync();
        Assert.Equal(2, applied.Count);
    }

    [Fact]
    public async Task MigrateAsync_IncrementalApply_OnlyNewScripts()
    {
        WriteMigration("V001__Init.sql", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);");
        await _sut.MigrateAsync();

        WriteMigration("V002__New.sql", "CREATE TABLE t2 (id INTEGER PRIMARY KEY);");
        var results = await _sut.MigrateAsync();

        Assert.Single(results);
        Assert.Equal("V002", results[0].Version);
    }

    [Fact]
    public async Task MigrateAsync_CancellationToken_Respected()
    {
        WriteMigration("V001__Init.sql", "CREATE TABLE t1 (id INTEGER PRIMARY KEY);");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _sut.MigrateAsync(cts.Token));
    }

    [Fact]
    public async Task MigrateAsync_MissingDirectory_ReturnsEmpty()
    {
        Directory.Delete(_migrationsDir, true);
        var results = await _sut.MigrateAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task MigrateAsync_ComplexSql_Works()
    {
        var sql = """
            CREATE TABLE config (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                key TEXT NOT NULL UNIQUE,
                value TEXT,
                updated_at TEXT DEFAULT (datetime('now'))
            );
            CREATE INDEX idx_config_key ON config(key);
            INSERT INTO config (key, value) VALUES ('version', '1.0.0');
            """;
        WriteMigration("V001__Complex.sql", sql);

        var results = await _sut.MigrateAsync();
        Assert.Single(results);
        Assert.True(results[0].Success);

        // Verify data was inserted
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM config WHERE key = 'version'";
        var val = cmd.ExecuteScalar();
        Assert.Equal("1.0.0", val);
    }

    private void WriteMigration(string fileName, string sql)
    {
        File.WriteAllText(Path.Combine(_migrationsDir, fileName), sql);
    }
}
