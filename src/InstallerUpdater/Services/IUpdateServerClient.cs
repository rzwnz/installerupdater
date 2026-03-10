using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

/// <summary>
/// Communicates with the Astra Linux update server to check and download updates.
/// </summary>
public interface IUpdateServerClient
{
    /// <summary>Check the update server for the latest available version.</summary>
    Task<UpdateManifest?> CheckForUpdateAsync(CancellationToken ct = default);

    /// <summary>Download the installer package to the local download directory.</summary>
    Task<string> DownloadUpdateAsync(UpdateManifest manifest, string downloadDir, CancellationToken ct = default);
}
