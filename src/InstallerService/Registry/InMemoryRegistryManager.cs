using Microsoft.Extensions.Logging;

namespace InstallerService.Configuration;

/// <summary>
/// In-memory registry manager for testing on non-Windows platforms.
/// Stores values in a dictionary instead of the actual Windows Registry.
/// </summary>
public sealed class InMemoryRegistryManager : IRegistryManager
{
    private readonly ILogger<InMemoryRegistryManager> _logger;
    private readonly Dictionary<string, Dictionary<string, object>> _store = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryRegistryManager(ILogger<InMemoryRegistryManager> logger)
    {
        _logger = logger;
    }

    private string MakeKey(string keyPath, string valueName) => $"{keyPath}\\{valueName}";

    public string? ReadString(string keyPath, string valueName)
    {
        if (_store.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out var val))
            return val as string;
        return null;
    }

    public int? ReadDWord(string keyPath, string valueName)
    {
        if (_store.TryGetValue(keyPath, out var values) && values.TryGetValue(valueName, out var val))
            return val is int i ? i : null;
        return null;
    }

    public void WriteString(string keyPath, string valueName, string value)
    {
        EnsureKey(keyPath)[valueName] = value;
        _logger.LogDebug("InMemory: wrote string {KeyPath}\\{ValueName} = {Value}", keyPath, valueName, value);
    }

    public void WriteDWord(string keyPath, string valueName, int value)
    {
        EnsureKey(keyPath)[valueName] = value;
        _logger.LogDebug("InMemory: wrote DWORD {KeyPath}\\{ValueName} = {Value}", keyPath, valueName, value);
    }

    public bool DeleteValue(string keyPath, string valueName)
    {
        if (_store.TryGetValue(keyPath, out var values))
            return values.Remove(valueName);
        return false;
    }

    public bool KeyExists(string keyPath)
    {
        return _store.ContainsKey(keyPath);
    }

    public IReadOnlyList<string> GetValueNames(string keyPath)
    {
        if (_store.TryGetValue(keyPath, out var values))
            return values.Keys.ToList();
        return Array.Empty<string>();
    }

    private Dictionary<string, object> EnsureKey(string keyPath)
    {
        if (!_store.TryGetValue(keyPath, out var values))
        {
            values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _store[keyPath] = values;
        }
        return values;
    }
}
