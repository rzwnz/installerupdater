namespace InstallerUpdater.Services;

public interface IInstalledVersionProvider
{
    string? GetInstalledVersion();
}