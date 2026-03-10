using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using InstallerService.Models;

namespace InstallerService.Database;

/// <summary>
/// Runs sequential SQL migration scripts against a local SQLite database.
/// Migration scripts are embedded as resources or read from the Migrations folder.
/// Tracks applied versions in a __migrations table.
/// </summary>
public sealed class SqliteMigrator : IDatabaseMigrator
{
    private readonly string _connectionString;
    private readonly string _migrationsPath;
    private readonly ILogger<SqliteMigrator> _logger;

    public SqliteMigrator(string connectionString, string migrationsPath, ILogger<SqliteMigrator> logger)
    {
        _connectionString = connectionString;
        _migrationsPath = migrationsPath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MigrationRecord>> MigrateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await EnsureMigrationsTableAsync(connection, ct);

        var applied = await GetAppliedVersionsAsync(connection, ct);
        var scripts = GetPendingScripts(applied);
        var results = new List<MigrationRecord>();

        foreach (var (version, description, sql) in scripts)
        {
            ct.ThrowIfCancellationRequested();
            var record = new MigrationRecord
            {
                Version = version,
                Description = description,
                Script = sql,
                AppliedAt = DateTime.UtcNow
            };

            await using var tx = await connection.BeginTransactionAsync(ct);
            try
            {
                await using var cmd = connection.CreateCommand();
                cmd.Transaction = (SqliteTransaction)tx;
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync(ct);

                record.Success = true;
                await RecordMigrationAsync(connection, (SqliteTransaction)tx, record, ct);
                await tx.CommitAsync(ct);
                _logger.LogInformation("Applied migration {Version}: {Description}", version, description);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                record.Success = false;
                record.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Migration {Version} failed", version);
                throw new MigrationException($"Migration {version} failed: {ex.Message}", ex);
            }

            results.Add(record);
        }

        return results;
    }

    public async Task<string?> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureMigrationsTableAsync(connection, ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM __migrations ORDER BY id DESC LIMIT 1";
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task<IReadOnlyList<MigrationRecord>> GetAppliedMigrationsAsync(CancellationToken ct = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureMigrationsTableAsync(connection, ct);

        var records = new List<MigrationRecord>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, version, description, applied_at, success, error_message FROM __migrations ORDER BY id";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new MigrationRecord
            {
                Id = reader.GetInt32(0),
                Version = reader.GetString(1),
                Description = reader.GetString(2),
                AppliedAt = DateTime.Parse(reader.GetString(3)),
                Success = reader.GetBoolean(4),
                ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return records;
    }

    private async Task EnsureMigrationsTableAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS __migrations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                version TEXT NOT NULL UNIQUE,
                description TEXT NOT NULL DEFAULT '',
                script TEXT NOT NULL DEFAULT '',
                applied_at TEXT NOT NULL,
                success INTEGER NOT NULL DEFAULT 1,
                error_message TEXT
            )
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<HashSet<string>> GetAppliedVersionsAsync(SqliteConnection connection, CancellationToken ct)
    {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM __migrations WHERE success = 1";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            versions.Add(reader.GetString(0));
        }
        return versions;
    }

    private List<(string Version, string Description, string Sql)> GetPendingScripts(HashSet<string> applied)
    {
        var pending = new List<(string, string, string)>();

        if (!Directory.Exists(_migrationsPath))
        {
            _logger.LogWarning("Migrations directory not found: {Path}", _migrationsPath);
            return pending;
        }

        var files = Directory.GetFiles(_migrationsPath, "V*.sql")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            // Expected format: V001__Description_here.sql
            var parts = fileName.Split("__", 2);
            var version = parts[0]; // V001
            var description = parts.Length > 1 ? parts[1].Replace('_', ' ') : fileName;

            if (!applied.Contains(version))
            {
                var sql = File.ReadAllText(file);
                pending.Add((version, description, sql));
            }
        }

        return pending;
    }

    private static async Task RecordMigrationAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        MigrationRecord record,
        CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO __migrations (version, description, script, applied_at, success, error_message)
            VALUES (@version, @description, @script, @applied_at, @success, @error_message)
            """;
        cmd.Parameters.AddWithValue("@version", record.Version);
        cmd.Parameters.AddWithValue("@description", record.Description);
        cmd.Parameters.AddWithValue("@script", record.Script);
        cmd.Parameters.AddWithValue("@applied_at", record.AppliedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@success", record.Success ? 1 : 0);
        cmd.Parameters.AddWithValue("@error_message", (object?)record.ErrorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Thrown when a database migration fails.
/// </summary>
public sealed class MigrationException : Exception
{
    public MigrationException(string message) : base(message) { }
    public MigrationException(string message, Exception inner) : base(message, inner) { }
}
