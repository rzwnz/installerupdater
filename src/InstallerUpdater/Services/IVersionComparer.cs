namespace InstallerUpdater.Services;

public interface IVersionComparer
{
    bool IsNewer(string candidate, string current);
    bool IsAtLeast(string current, string minimum);
}