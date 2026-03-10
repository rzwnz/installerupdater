namespace InstallerUpdater.Models;

/// <summary>
/// Represents version information returned by the update server.
/// </summary>
public sealed class UpdateManifest
{
    /// <summary>Latest available version string (e.g. "1.2.0").</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>Download URL for the installer package.</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the installer for integrity verification.</summary>
    public string Sha256Hash { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; set; }

    /// <summary>Release notes / changelog.</summary>
    public string? ReleaseNotes { get; set; }

    /// <summary>Whether this update is mandatory.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>Minimum version required to apply this update (for incremental updates).</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>Timestamp when this version was published.</summary>
    public DateTime PublishedAt { get; set; }
}
