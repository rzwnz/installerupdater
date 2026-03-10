using Microsoft.Win32;
using Microsoft.Extensions.Logging;

namespace InstallerService.Configuration;

/// <summary>
/// Windows Registry implementation using Microsoft.Win32.
/// Operates under HKEY_LOCAL_MACHINE by default.
/// </summary>
public sealed class WindowsRegistryManager : IRegistryManager
{
    private readonly ILogger<WindowsRegistryManager> _logger;

    public WindowsRegistryManager(ILogger<WindowsRegistryManager> logger)
    {
        _logger = logger;
    }

    public string? ReadString(string keyPath, string valueName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValue(valueName) as string;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read registry string {KeyPath}\\{ValueName}", keyPath, valueName);
            return null;
        }
    }

    public int? ReadDWord(string keyPath, string valueName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            var value = key?.GetValue(valueName);
            return value is int intVal ? intVal : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read registry DWORD {KeyPath}\\{ValueName}", keyPath, valueName);
            return null;
        }
    }

    public void WriteString(string keyPath, string valueName, string value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath, true);
            key.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.String);
            _logger.LogInformation("Wrote registry string {KeyPath}\\{ValueName} = {Value}", keyPath, valueName, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write registry string {KeyPath}\\{ValueName}", keyPath, valueName);
            throw;
        }
    }

    public void WriteDWord(string keyPath, string valueName, int value)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath, true);
            key.SetValue(valueName, value, Microsoft.Win32.RegistryValueKind.DWord);
            _logger.LogInformation("Wrote registry DWORD {KeyPath}\\{ValueName} = {Value}", keyPath, valueName, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write registry DWORD {KeyPath}\\{ValueName}", keyPath, valueName);
            throw;
        }
    }

    public bool DeleteValue(string keyPath, string valueName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath, true);
            if (key is null) return false;
            key.DeleteValue(valueName, false);
            _logger.LogInformation("Deleted registry value {KeyPath}\\{ValueName}", keyPath, valueName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete registry value {KeyPath}\\{ValueName}", keyPath, valueName);
            return false;
        }
    }

    public bool KeyExists(string keyPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            return key is not null;
        }
        catch
        {
            return false;
        }
    }

    public IReadOnlyList<string> GetValueNames(string keyPath)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(keyPath);
            return key?.GetValueNames() ?? Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list registry values under {KeyPath}", keyPath);
            return Array.Empty<string>();
        }
    }
}
