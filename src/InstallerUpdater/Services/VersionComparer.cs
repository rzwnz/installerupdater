namespace InstallerUpdater.Services;

public sealed class VersionComparer : IVersionComparer
{
    public bool IsNewer(string candidate, string current)
    {
        if (Version.TryParse(NormalizeVersion(candidate), out var candidateVer) &&
            Version.TryParse(NormalizeVersion(current), out var currentVer))
        {
            return candidateVer > currentVer;
        }

        return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    public bool IsAtLeast(string current, string minimum)
    {
        if (Version.TryParse(NormalizeVersion(current), out var currentVer) &&
            Version.TryParse(NormalizeVersion(minimum), out var minimumVer))
        {
            return currentVer >= minimumVer;
        }

        return string.Compare(current, minimum, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeVersion(string version)
    {
        var parts = version.Split('.');
        return parts.Length switch
        {
            1 => $"{version}.0",
            _ => version
        };
    }
}