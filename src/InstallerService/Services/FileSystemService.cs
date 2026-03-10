using Microsoft.Extensions.Logging;

namespace InstallerService.Services;

/// <summary>
/// Concrete implementation of file system operations.
/// ACL operations use System.IO and are best-effort on non-Windows platforms.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    private readonly ILogger<FileSystemService> _logger;

    public FileSystemService(ILogger<FileSystemService> logger)
    {
        _logger = logger;
    }

    public void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _logger.LogInformation("Created directory: {Path}", path);
        }
    }

    public void SetDirectoryPermissions(string path, string identity, bool fullControl)
    {
        // Windows ACL operations -- on non-Windows this is a no-op logged as warning
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogWarning("ACL permissions not supported on this platform. Skipping for {Path}", path);
            return;
        }

        try
        {
            // Use icacls via process invocation for cross-platform safety
            var permission = fullControl ? "(OI)(CI)F" : "(OI)(CI)RX";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "icacls",
                Arguments = $"\"{path}\" /grant \"{identity}\":{permission} /T /C",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(psi);
            process?.WaitForExit(30_000);
            _logger.LogInformation("Set permissions on {Path} for {Identity} (fullControl={FullControl})",
                path, identity, fullControl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set permissions on {Path}", path);
            throw;
        }
    }

    public void CopyFile(string source, string destination, bool overwrite = true)
    {
        var destDir = Path.GetDirectoryName(destination);
        if (destDir is not null && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
        }
        File.Copy(source, destination, overwrite);
        _logger.LogDebug("Copied {Source} -> {Destination}", source, destination);
    }

    public bool DeleteFile(string path)
    {
        if (!File.Exists(path)) return false;
        File.Delete(path);
        _logger.LogDebug("Deleted file: {Path}", path);
        return true;
    }

    public long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return new DirectoryInfo(path)
            .EnumerateFiles("*", SearchOption.AllDirectories)
            .Sum(f => f.Length);
    }

    public int CleanOldFiles(string directory, TimeSpan maxAge, string pattern = "*")
    {
        if (!Directory.Exists(directory)) return 0;

        var cutoff = DateTime.UtcNow - maxAge;
        var count = 0;

        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                try
                {
                    File.Delete(file);
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not delete old file: {File}", file);
                }
            }
        }

        _logger.LogInformation("Cleaned {Count} old files from {Directory}", count, directory);
        return count;
    }

    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);
}
