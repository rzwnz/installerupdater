using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

/// <summary>
/// Background worker that periodically polls the Astra Linux update server,
/// and applies updates when a newer version is available.
/// </summary>
public sealed class UpdateWorker : BackgroundService
{
    private readonly ILogger<UpdateWorker> _logger;
    private readonly IUpdateServerClient _serverClient;
    private readonly IUpdateApplier _applier;
    private readonly IInstalledVersionProvider _installedVersionProvider;
    private readonly IVersionComparer _versionComparer;
    private readonly UpdaterOptions _options;

    public UpdateWorker(
        ILogger<UpdateWorker> logger,
        IUpdateServerClient serverClient,
        IUpdateApplier applier,
        IInstalledVersionProvider installedVersionProvider,
        IVersionComparer versionComparer,
        IOptions<UpdaterOptions> options)
    {
        _logger = logger;
        _serverClient = serverClient;
        _applier = applier;
        _installedVersionProvider = installedVersionProvider;
        _versionComparer = versionComparer;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("InstallerUpdater worker started. Polling every {Interval} minutes",
            _options.PollIntervalMinutes);

        // Initial delay to let the system stabilize after boot
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndApplyUpdateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update check cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.PollIntervalMinutes), stoppingToken);
        }
    }

    internal async Task CheckAndApplyUpdateAsync(CancellationToken ct)
    {
        _logger.LogInformation("Checking for updates...");

        var manifest = await _serverClient.CheckForUpdateAsync(ct);
        if (manifest is null)
        {
            _logger.LogInformation("No update information received");
            return;
        }

        var currentVersion = _installedVersionProvider.GetInstalledVersion() ?? "0.0.0";
        if (!_versionComparer.IsNewer(manifest.Version, currentVersion))
        {
            _logger.LogInformation("Current version {Current} is up to date (server: {Server})",
                currentVersion, manifest.Version);
            return;
        }

        // Check minimum version constraint
        if (!string.IsNullOrEmpty(manifest.MinimumVersion) &&
            !_versionComparer.IsAtLeast(currentVersion, manifest.MinimumVersion))
        {
            _logger.LogWarning(
                "Current version {Current} is below minimum required {Min} for this update",
                currentVersion, manifest.MinimumVersion);
            return;
        }

        _logger.LogInformation("Update available: {Current} -> {New}", currentVersion, manifest.Version);

        // Download
        var installerPath = await _serverClient.DownloadUpdateAsync(manifest, _options.DownloadDirectory, ct);
        _logger.LogInformation("Update downloaded to {Path}", installerPath);

        // Apply
        var result = await _applier.ApplyUpdateAsync(installerPath, manifest, ct);
        if (result.Success)
        {
            _logger.LogInformation("Successfully updated from {Old} to {New}",
                result.PreviousVersion, result.InstalledVersion);
        }
        else
        {
            _logger.LogError("Update failed at phase {Phase}: {Error}", result.Phase, result.ErrorMessage);
        }
    }

}
