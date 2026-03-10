using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

/// <summary>
/// HTTP client for the Astra Linux update server.
/// Checks /api/updates/latest for manifest and downloads the installer binary.
/// </summary>
public sealed class UpdateServerClient : IUpdateServerClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UpdateServerClient> _logger;
    private readonly UpdaterOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public UpdateServerClient(HttpClient httpClient, ILogger<UpdateServerClient> logger, IOptions<UpdaterOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.UpdateServerUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.HttpTimeoutSeconds);
    }

    public async Task<UpdateManifest?> CheckForUpdateAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/updates/latest", ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check returned status {StatusCode}", (int)response.StatusCode);
                return null;
            }

            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(JsonOptions, ct);
            _logger.LogInformation("Update server reports latest version: {Version}", manifest?.Version);
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
            return null;
        }
    }

    public async Task<string> DownloadUpdateAsync(UpdateManifest manifest, string downloadDir, CancellationToken ct = default)
    {
        if (!Directory.Exists(downloadDir))
            Directory.CreateDirectory(downloadDir);

        var fileName = $"InstallerUpdaterSetup-{manifest.Version}.exe";
        var filePath = Path.Combine(downloadDir, fileName);

        _logger.LogInformation("Downloading update {Version} ({Size} bytes) to {Path}",
            manifest.Version, manifest.FileSize, filePath);

        for (int attempt = 1; attempt <= _options.MaxRetries; attempt++)
        {
            try
            {
                var downloadUrl = manifest.DownloadUrl.StartsWith("http")
                    ? manifest.DownloadUrl
                    : $"{_options.UpdateServerUrl.TrimEnd('/')}/{manifest.DownloadUrl.TrimStart('/')}";

                using var response = await _httpClient.GetAsync(downloadUrl,
                    HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
                await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 81920, useAsync: true);
                await contentStream.CopyToAsync(fileStream, ct);

                // Verify SHA-256 integrity
                if (!string.IsNullOrEmpty(manifest.Sha256Hash))
                {
                    var actualHash = await ComputeFileHashAsync(filePath, ct);
                    if (!string.Equals(actualHash, manifest.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(filePath);
                        throw new InvalidOperationException(
                            $"Hash mismatch: expected {manifest.Sha256Hash}, got {actualHash}");
                    }
                    _logger.LogInformation("SHA-256 verification passed for {FileName}", fileName);
                }

                return filePath;
            }
            catch (Exception ex) when (attempt < _options.MaxRetries)
            {
                _logger.LogWarning(ex, "Download attempt {Attempt}/{MaxRetries} failed, retrying...",
                    attempt, _options.MaxRetries);
                await Task.Delay(TimeSpan.FromSeconds(attempt * 5), ct);
            }
        }

        throw new InvalidOperationException($"Failed to download update after {_options.MaxRetries} attempts");
    }

    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }
}
