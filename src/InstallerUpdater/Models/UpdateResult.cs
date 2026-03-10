namespace InstallerUpdater.Models;

/// <summary>
/// Result of an update check or application attempt.
/// </summary>
public sealed class UpdateResult
{
    public bool Success { get; set; }
    public string? InstalledVersion { get; set; }
    public string? PreviousVersion { get; set; }
    public string? ErrorMessage { get; set; }
    public UpdatePhase Phase { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public enum UpdatePhase
{
    CheckingForUpdates,
    Downloading,
    VerifyingIntegrity,
    StoppingService,
    ApplyingUpdate,
    StartingService,
    Completed,
    Failed,
    RollingBack
}
