using System.Diagnostics;
using System.ServiceProcess;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

/// <summary>
/// Applies updates by managing the Windows service lifecycle and running the Inno Setup installer.
/// 1. Stops InstallerService
/// 2. Backs up the current installation
/// 3. Runs the Inno Setup installer silently
/// 4. Restarts InstallerService
/// </summary>
public sealed class UpdateApplier : IUpdateApplier
{
    private readonly ILogger<UpdateApplier> _logger;
    private readonly UpdaterOptions _options;
    private readonly IInstalledVersionProvider _installedVersionProvider;
    private static readonly TimeSpan ServiceTimeout = TimeSpan.FromSeconds(60);

    public UpdateApplier(
        ILogger<UpdateApplier> logger,
        IOptions<UpdaterOptions> options,
        IInstalledVersionProvider installedVersionProvider)
    {
        _logger = logger;
        _options = options.Value;
        _installedVersionProvider = installedVersionProvider;
    }

    public async Task<UpdateResult> ApplyUpdateAsync(string installerPath, UpdateManifest manifest, CancellationToken ct = default)
    {
        var result = new UpdateResult
        {
            PreviousVersion = _installedVersionProvider.GetInstalledVersion(),
            Phase = UpdatePhase.StoppingService
        };

        try
        {
            // 1. Stop the target service
            _logger.LogInformation("Stopping service {ServiceName}", _options.TargetServiceName);
            result.Phase = UpdatePhase.StoppingService;
            await StopServiceAsync(_options.TargetServiceName, ct);

            // 2. Create backup of current installation
            _logger.LogInformation("Creating backup before update");
            await CreateBackupAsync(ct);

            // 3. Run the installer
            _logger.LogInformation("Applying update {Version} from {Installer}", manifest.Version, installerPath);
            result.Phase = UpdatePhase.ApplyingUpdate;
            await RunInstallerAsync(installerPath, ct);

            // 4. Restart the service
            _logger.LogInformation("Starting service {ServiceName}", _options.TargetServiceName);
            result.Phase = UpdatePhase.StartingService;
            await StartServiceAsync(_options.TargetServiceName, ct);

            result.Success = true;
            result.InstalledVersion = manifest.Version;
            result.Phase = UpdatePhase.Completed;
            _logger.LogInformation("Update to {Version} completed successfully", manifest.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update failed at phase {Phase}", result.Phase);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Phase = UpdatePhase.Failed;

            // Attempt rollback
            try
            {
                await RollbackAsync(ct);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "Rollback also failed");
            }
        }

        return result;
    }

    public async Task<UpdateResult> RollbackAsync(CancellationToken ct = default)
    {
        var result = new UpdateResult { Phase = UpdatePhase.RollingBack };

        try
        {
            var backupDir = Path.Combine(_options.DownloadDirectory, "backup");
            if (!Directory.Exists(backupDir))
            {
                result.Success = false;
                result.ErrorMessage = "No backup found for rollback";
                result.Phase = UpdatePhase.Failed;
                return result;
            }

            _logger.LogInformation("Rolling back from backup");

            await StopServiceAsync(_options.TargetServiceName, ct);

            // Restore backup files
            var installDir = Path.GetDirectoryName(_options.InstallerPath) ?? _options.DownloadDirectory;
            CopyDirectory(backupDir, installDir);

            await StartServiceAsync(_options.TargetServiceName, ct);

            result.Success = true;
            result.Phase = UpdatePhase.Completed;
            _logger.LogInformation("Rollback completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Phase = UpdatePhase.Failed;
        }

        return result;
    }

    internal static async Task StopServiceAsync(string serviceName, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return;

        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Running ||
            sc.Status == ServiceControllerStatus.StartPending)
        {
            sc.Stop();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Stopped, ServiceTimeout), ct);
        }
    }

    internal static async Task StartServiceAsync(string serviceName, CancellationToken ct)
    {
        if (!OperatingSystem.IsWindows()) return;

        using var sc = new ServiceController(serviceName);
        if (sc.Status == ServiceControllerStatus.Stopped ||
            sc.Status == ServiceControllerStatus.StopPending)
        {
            sc.Start();
            await Task.Run(() => sc.WaitForStatus(ServiceControllerStatus.Running, ServiceTimeout), ct);
        }
    }

    private async Task RunInstallerAsync(string installerPath, CancellationToken ct)
    {
        if (!File.Exists(installerPath))
            throw new FileNotFoundException("Installer not found", installerPath);

        var args = _options.SilentInstall
            ? "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG"
            : "/SILENT /LOG";

        _logger.LogInformation("Running installer: {Path} {Args}", installerPath, args);

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start installer process");

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            var stderr = await process.StandardError.ReadToEndAsync(ct);
            throw new InvalidOperationException(
                $"Installer exited with code {process.ExitCode}: {stderr}");
        }

        _logger.LogInformation("Installer completed with exit code 0");
    }

    private async Task CreateBackupAsync(CancellationToken ct)
    {
        var backupDir = Path.Combine(_options.DownloadDirectory, "backup");
        if (Directory.Exists(backupDir))
            Directory.Delete(backupDir, true);
        Directory.CreateDirectory(backupDir);

        var installDir = _options.DownloadDirectory;
        if (Directory.Exists(installDir))
        {
            await Task.Run(() => CopyDirectory(installDir, backupDir, excludePattern: "backup"), ct);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir, string? excludePattern = null)
    {
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, fileName), true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            if (excludePattern != null && dirName.Contains(excludePattern, StringComparison.OrdinalIgnoreCase))
                continue;
            CopyDirectory(dir, Path.Combine(destDir, dirName), excludePattern);
        }
    }
}
