namespace InstallerService.Models;

/// <summary>
/// Represents the result of a Tomcat heartbeat or auth check.
/// </summary>
public sealed class HeartbeatResult
{
    public bool IsAlive { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int StatusCode { get; set; }
}
