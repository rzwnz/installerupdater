using InstallerService.Models;

namespace InstallerService.Database;

/// <summary>
/// Interface for running database schema migrations against SQLite or PostgreSQL.
/// </summary>
public interface IDatabaseMigrator
{
    /// <summary>Run all pending migrations. Returns the list of applied migrations.</summary>
    Task<IReadOnlyList<MigrationRecord>> MigrateAsync(CancellationToken ct = default);

    /// <summary>Get the current schema version.</summary>
    Task<string?> GetCurrentVersionAsync(CancellationToken ct = default);

    /// <summary>List all applied migrations.</summary>
    Task<IReadOnlyList<MigrationRecord>> GetAppliedMigrationsAsync(CancellationToken ct = default);
}
