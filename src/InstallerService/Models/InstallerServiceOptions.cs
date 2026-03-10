namespace InstallerService.Models;

/// <summary>
/// Represents the application configuration for InstallerService.
/// </summary>
public sealed class InstallerServiceOptions
{
    public const string SectionName = "InstallerService";

    public const string DatabaseProviderSqlite = "sqlite";
    public const string DatabaseProviderPostgres = "postgres";

    /// <summary>Base URL of the Tomcat server for auth and messaging.</summary>
    public string TomcatBaseUrl { get; set; } = "http://localhost:8080";

    /// <summary>Interval in seconds between heartbeat checks to Tomcat.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>Active Directory domain used for authentication.</summary>
    public string AdDomain { get; set; } = string.Empty;

    /// <summary>Local SQLite database path for offline state.</summary>
    public string LocalDbPath { get; set; } = @"C:\ProgramData\InstallerUpdater\installer.db";

    /// <summary>PostgreSQL connection string for remote schema operations.</summary>
    public string PostgresConnectionString { get; set; } = string.Empty;

    /// <summary>Database provider to use for migrations: sqlite or postgres.</summary>
    public string DatabaseProvider { get; set; } = DatabaseProviderSqlite;

    /// <summary>Directory where the service stores working files.</summary>
    public string WorkingDirectory { get; set; } = @"C:\ProgramData\InstallerUpdater";

    /// <summary>Registry base path for service configuration.</summary>
    public string RegistryBasePath { get; set; } = @"SOFTWARE\rzwnz\InstallerService";
}
