namespace InstallerService.Models;

/// <summary>
/// Describes one registry entry the service reads or writes.
/// </summary>
public sealed class RegistryEntry
{
    public string KeyPath { get; set; } = string.Empty;
    public string ValueName { get; set; } = string.Empty;
    public object? Value { get; set; }
    public RegistryValueKind Kind { get; set; } = RegistryValueKind.String;
}

public enum RegistryValueKind
{
    String,
    DWord,
    QWord,
    Binary,
    MultiString,
    ExpandString
}
