using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using InstallerUpdater.Models;

namespace InstallerUpdater.Services;

public sealed class RegistryInstalledVersionProvider : IInstalledVersionProvider
{
    private readonly ILogger<RegistryInstalledVersionProvider> _logger;
    private readonly UpdaterOptions _options;

    public RegistryInstalledVersionProvider(
        ILogger<RegistryInstalledVersionProvider> logger,
        IOptions<UpdaterOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public string? GetInstalledVersion()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(_options.RegistryBasePath);
                return key?.GetValue("Version") as string;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read installed version from registry");
        }

        return null;
    }
}