using Npgsql;
using Microsoft.Extensions.Logging;
using InstallerService.Models;

namespace InstallerService.Database;

/// <summary>
/// Runs sequential SQL migration scripts against a PostgreSQL database.
/// Tracks applied versions in a __migrations table.
/// </summary>
public sealed class PostgresMigrator : IDatabaseMigrator
{
    private readonly string _connectionString;
    private readonly string _migrationsPath;
    private readonly ILogger<PostgresMigrator> _logger;

    public PostgresMigrator(string connectionString, string migrationsPath, ILogger<PostgresMigrator> logger)
    {
        _connectionString = connectionString;
        _migrationsPath = migrationsPath;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MigrationRecord>> MigrateAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var connection = new NpgsqlConnection(_connectionString);
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
                await using var cmd = new NpgsqlCommand(sql, connection, (NpgsqlTransaction)tx);
                await cmd.ExecuteNonQueryAsync(ct);

                record.Success = true;
                await RecordMigrationAsync(connection, (NpgsqlTransaction)tx, record, ct);
                await tx.CommitAsync(ct);
                _logger.LogInformation("Applied PostgreSQL migration {Version}: {Description}", version, description);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                record.Success = false;
                record.ErrorMessage = ex.Message;
                _logger.LogError(ex, "PostgreSQL migration {Version} failed", version);
                throw new MigrationException($"PostgreSQL migration {version} failed: {ex.Message}", ex);
            }

            results.Add(record);
        }

        return results;
    }

    public async Task<string?> GetCurrentVersionAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureMigrationsTableAsync(connection, ct);

        await using var cmd = new NpgsqlCommand(
            "SELECT version FROM __migrations ORDER BY id DESC LIMIT 1", connection);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task<IReadOnlyList<MigrationRecord>> GetAppliedMigrationsAsync(CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await EnsureMigrationsTableAsync(connection, ct);

        var records = new List<MigrationRecord>();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, version, description, applied_at, success, error_message FROM __migrations ORDER BY id",
            connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            records.Add(new MigrationRecord
            {
                Id = reader.GetInt32(0),
                Version = reader.GetString(1),
                Description = reader.GetString(2),
                AppliedAt = reader.GetDateTime(3),
                Success = reader.GetBoolean(4),
                ErrorMessage = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }

        return records;
    }

    private static async Task EnsureMigrationsTableAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS __migrations (
                id SERIAL PRIMARY KEY,
                version TEXT NOT NULL UNIQUE,
                description TEXT NOT NULL DEFAULT '',
                script TEXT NOT NULL DEFAULT '',
                applied_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                success BOOLEAN NOT NULL DEFAULT TRUE,
                error_message TEXT
            )
            """, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<HashSet<string>> GetAppliedVersionsAsync(NpgsqlConnection connection, CancellationToken ct)
    {
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = new NpgsqlCommand("SELECT version FROM __migrations WHERE success = TRUE", connection);
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
            _logger.LogWarning("PostgreSQL migrations directory not found: {Path}", _migrationsPath);
            return pending;
        }

        var files = Directory.GetFiles(_migrationsPath, "V*.sql")
                             .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                             .ToList();

        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var parts = fileName.Split("__", 2);
            var version = parts[0];
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
        NpgsqlConnection connection,
        NpgsqlTransaction tx,
        MigrationRecord record,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            INSERT INTO __migrations (version, description, script, applied_at, success, error_message)
            VALUES (@version, @description, @script, @applied_at, @success, @error_message)
            """, connection, tx);
        cmd.Parameters.AddWithValue("@version", record.Version);
        cmd.Parameters.AddWithValue("@description", record.Description);
        cmd.Parameters.AddWithValue("@script", record.Script);
        cmd.Parameters.AddWithValue("@applied_at", record.AppliedAt);
        cmd.Parameters.AddWithValue("@success", record.Success);
        cmd.Parameters.AddWithValue("@error_message", (object?)record.ErrorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
