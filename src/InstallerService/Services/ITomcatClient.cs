using InstallerService.Models;

namespace InstallerService.Services;

/// <summary>
/// Communicates with Tomcat for authentication and message flashing.
/// </summary>
public interface ITomcatClient
{
    /// <summary>Perform a heartbeat check against Tomcat.</summary>
    Task<HeartbeatResult> HeartbeatAsync(CancellationToken ct = default);

    /// <summary>Validate credentials via Tomcat's AD-based auth endpoint.</summary>
    Task<bool> ValidateAuthAsync(string username, string domain, CancellationToken ct = default);

    /// <summary>Send a flash message to Tomcat for user notification.</summary>
    Task<bool> FlashMessageAsync(string message, string severity, CancellationToken ct = default);

    /// <summary>Report service status to Tomcat.</summary>
    Task ReportStatusAsync(string status, string? details = null, CancellationToken ct = default);
}
