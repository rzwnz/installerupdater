using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerService.Models;

namespace InstallerService.Services;

/// <summary>
/// HTTP-based client for communicating with the Tomcat backend server.
/// Handles heartbeat, auth validation, message flashing, and status reporting.
/// </summary>
public sealed class TomcatClient : ITomcatClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TomcatClient> _logger;
    private readonly InstallerServiceOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public TomcatClient(HttpClient httpClient, ILogger<TomcatClient> logger, IOptions<InstallerServiceOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.TomcatBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
    }

    public async Task<HeartbeatResult> HeartbeatAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/health", ct);
            return new HeartbeatResult
            {
                IsAlive = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                Message = response.IsSuccessStatusCode ? "OK" : response.ReasonPhrase,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tomcat heartbeat failed");
            return new HeartbeatResult
            {
                IsAlive = false,
                StatusCode = 0,
                Message = ex.Message,
                Timestamp = DateTime.UtcNow
            };
        }
    }

    public async Task<bool> ValidateAuthAsync(string username, string domain, CancellationToken ct = default)
    {
        try
        {
            var payload = new { username, domain };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/validate", payload, JsonOptions, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth validation failed for {Username}@{Domain}", username, domain);
            return false;
        }
    }

    public async Task<bool> FlashMessageAsync(string message, string severity, CancellationToken ct = default)
    {
        try
        {
            var payload = new { message, severity, timestamp = DateTime.UtcNow };
            var response = await _httpClient.PostAsJsonAsync("/api/flash", payload, JsonOptions, ct);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Flash message sent: [{Severity}] {Message}", severity, message);
                return true;
            }
            _logger.LogWarning("Flash message failed with status {StatusCode}", (int)response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send flash message");
            return false;
        }
    }

    public async Task ReportStatusAsync(string status, string? details = null, CancellationToken ct = default)
    {
        try
        {
            var payload = new
            {
                serviceName = "InstallerService",
                status,
                details,
                timestamp = DateTime.UtcNow,
                machineName = Environment.MachineName
            };
            await _httpClient.PostAsJsonAsync("/api/service-status", payload, JsonOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to report status to Tomcat");
        }
    }
}
