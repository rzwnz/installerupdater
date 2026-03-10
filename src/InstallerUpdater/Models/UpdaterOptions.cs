namespace InstallerUpdater.Models;

/// <summary>
/// Configuration options for the updater service.
/// </summary>
public sealed class UpdaterOptions
{
    public const string SectionName = "InstallerUpdater";

    /// <summary>Base URL of the Astra Linux update server.</summary>
    public string UpdateServerUrl { get; set; } = "http://update-server.local";

    /// <summary>Polling interval in minutes.</summary>
    public int PollIntervalMinutes { get; set; } = 60;

    /// <summary>Local directory for downloading update packages.</summary>
    public string DownloadDirectory { get; set; } = @"C:\ProgramData\InstallerUpdater\updates";

    /// <summary>Name of the Windows service to update.</summary>
    public string TargetServiceName { get; set; } = "InstallerService";

    /// <summary>Maximum number of retry attempts for downloads.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Registry path to read current installed version.</summary>
    public string RegistryBasePath { get; set; } = @"SOFTWARE\rzwnz\InstallerService";

    /// <summary>Timeout in seconds for HTTP requests to the update server.</summary>
    public int HttpTimeoutSeconds { get; set; } = 120;

    /// <summary>Whether to perform silent installation (no UI).</summary>
    public bool SilentInstall { get; set; } = true;

    /// <summary>Path to the installer executable (Inno Setup output).</summary>
    public string InstallerPath { get; set; } = @"C:\ProgramData\InstallerUpdater\updates\InstallerUpdaterSetup.exe";
}
