namespace InstallerService.Models;

/// <summary>
/// Tracks the current state of a database migration.
/// </summary>
public sealed class MigrationRecord
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Script { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
