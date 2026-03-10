using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

/// <summary>
/// Manages the lifecycle of applying updates: stop service, run installer, restart.
/// </summary>
public interface IUpdateApplier
{
    /// <summary>
    /// Apply the downloaded update. Stops the target service, runs the installer,
    /// and restarts the service. Returns the result of the operation.
    /// </summary>
    Task<UpdateResult> ApplyUpdateAsync(string installerPath, UpdateManifest manifest, CancellationToken ct = default);

    /// <summary>
    /// Rollback to the previous version if backup exists.
    /// </summary>
    Task<UpdateResult> RollbackAsync(CancellationToken ct = default);
}
