namespace InstallerService.Configuration;

/// <summary>
/// Manages reading/writing configuration from the Windows Registry.
/// Platform-agnostic interface to facilitate testing on non-Windows hosts.
/// </summary>
public interface IRegistryManager
{
    /// <summary>Read a registry string value. Returns null if not found.</summary>
    string? ReadString(string keyPath, string valueName);

    /// <summary>Read a registry DWORD (int) value. Returns null if not found.</summary>
    int? ReadDWord(string keyPath, string valueName);

    /// <summary>Write a string value to the registry.</summary>
    void WriteString(string keyPath, string valueName, string value);

    /// <summary>Write a DWORD value to the registry.</summary>
    void WriteDWord(string keyPath, string valueName, int value);

    /// <summary>Delete a specific value from a registry key.</summary>
    bool DeleteValue(string keyPath, string valueName);

    /// <summary>Check whether a registry key exists.</summary>
    bool KeyExists(string keyPath);

    /// <summary>List all value names under a registry key.</summary>
    IReadOnlyList<string> GetValueNames(string keyPath);
}
