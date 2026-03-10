using InstallerUpdater.Services;

namespace InstallerService.Tests.Services;

/// <summary>
/// Tests for UpdateWorker's static version comparison methods.
/// These are internal and tested via InternalsVisibleTo or by making them public for testing.
/// Since we reference them as internal, we test the logic directly here.
/// </summary>
public class VersionComparisonTests
{
    private readonly VersionComparer _comparer = new();

    [Theory]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.0", "2.0.0", false)]
    [InlineData("10.0.0", "9.0.0", true)]
    [InlineData("1.10.0", "1.9.0", true)]
    public void IsNewerVersion_ReturnsCorrectResult(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, _comparer.IsNewer(candidate, current));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", true)]
    [InlineData("2.0.0", "1.0.0", true)]
    [InlineData("0.9.0", "1.0.0", false)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.0.0", "1.0.1", false)]
    public void IsAtLeastVersion_ReturnsCorrectResult(string current, string minimum, bool expected)
    {
        Assert.Equal(expected, _comparer.IsAtLeast(current, minimum));
    }

    [Theory]
    [InlineData("1", "0.9.0", true)]
    [InlineData("2", "1", true)]
    [InlineData("1", "1", false)]
    public void IsNewerVersion_SinglePartVersion_Works(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, _comparer.IsNewer(candidate, current));
    }

    [Theory]
    [InlineData("1.2.3.4", "1.2.3.3", true)]
    [InlineData("1.2.3.4", "1.2.3.4", false)]
    public void IsNewerVersion_FourPartVersion_Works(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, _comparer.IsNewer(candidate, current));
    }
}
