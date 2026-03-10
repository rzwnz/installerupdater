using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerService.Configuration;
using InstallerService.Database;
using InstallerService.Models;

namespace InstallerService.Services;

/// <summary>
/// Main background worker for InstallerService.
/// Performs periodic heartbeats to Tomcat, runs startup migrations,
/// manages registry state, and reports status.
/// </summary>
public sealed class InstallerWorker : BackgroundService
{
    private readonly ILogger<InstallerWorker> _logger;
    private readonly ITomcatClient _tomcatClient;
    private readonly IRegistryManager _registryManager;
    private readonly IFileSystemService _fileSystem;
    private readonly IDatabaseMigrator _databaseMigrator;
    private readonly InstallerServiceOptions _options;

    public InstallerWorker(
        ILogger<InstallerWorker> logger,
        ITomcatClient tomcatClient,
        IRegistryManager registryManager,
        IFileSystemService fileSystem,
        IDatabaseMigrator databaseMigrator,
        IOptions<InstallerServiceOptions> options)
    {
        _logger = logger;
        _tomcatClient = tomcatClient;
        _registryManager = registryManager;
        _fileSystem = fileSystem;
        _databaseMigrator = databaseMigrator;
        _options = options.Value;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InstallerService starting on {Machine}", Environment.MachineName);

        // Ensure working directory exists
        _fileSystem.EnsureDirectory(_options.WorkingDirectory);

        // Store service version in registry
        var version = typeof(InstallerWorker).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        _registryManager.WriteString(_options.RegistryBasePath, "Version", version);
        _registryManager.WriteString(_options.RegistryBasePath, "InstallPath", _options.WorkingDirectory);

        // Run database migrations on startup
        await RunStartupMigrationsAsync(cancellationToken);

        // Report start to Tomcat
        await _tomcatClient.ReportStatusAsync("Starting", $"Version {version}", cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InstallerService worker running");
        _registryManager.WriteString(_options.RegistryBasePath, "Status", "Running");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var heartbeat = await _tomcatClient.HeartbeatAsync(stoppingToken);
                _registryManager.WriteString(_options.RegistryBasePath, "LastHeartbeat",
                    heartbeat.Timestamp.ToString("O"));

                if (!heartbeat.IsAlive)
                {
                    _logger.LogWarning("Tomcat heartbeat failed: {Message}", heartbeat.Message);
                    _registryManager.WriteString(_options.RegistryBasePath, "TomcatStatus", "Unreachable");
                }
                else
                {
                    _registryManager.WriteString(_options.RegistryBasePath, "TomcatStatus", "OK");
                }

                // Clean old log files (older than 30 days)
                var logsDir = Path.Combine(_options.WorkingDirectory, "logs");
                if (_fileSystem.DirectoryExists(logsDir))
                {
                    _fileSystem.CleanOldFiles(logsDir, TimeSpan.FromDays(30), "*.log");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during heartbeat cycle");
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.HeartbeatIntervalSeconds), stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("InstallerService stopping");
        _registryManager.WriteString(_options.RegistryBasePath, "Status", "Stopped");

        await _tomcatClient.ReportStatusAsync("Stopped", null, cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task RunStartupMigrationsAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Running database migrations...");
            var applied = await _databaseMigrator.MigrateAsync(ct);
            if (applied.Count > 0)
            {
                _logger.LogInformation("Applied {Count} migration(s)", applied.Count);
                var currentVersion = await _databaseMigrator.GetCurrentVersionAsync(ct);
                _registryManager.WriteString(_options.RegistryBasePath, "DbVersion", currentVersion ?? "unknown");
            }
            else
            {
                _logger.LogInformation("No pending migrations");
            }
        }
        catch (MigrationException ex)
        {
            _logger.LogError(ex, "Migration failed during startup. Service will continue but database may be outdated.");
            await _tomcatClient.FlashMessageAsync(
                $"Database migration failed: {ex.Message}", "error", ct);
        }
    }
}
