using InstallerUpdater.Models;

namespace InstallerService.Tests.Services;

public class UpdaterModelTests
{
    [Fact]
    public void UpdaterOptions_DefaultValues()
    {
        var opts = new UpdaterOptions();

        Assert.Equal("http://update-server.local", opts.UpdateServerUrl);
        Assert.Equal(60, opts.PollIntervalMinutes);
        Assert.Equal(@"C:\ProgramData\InstallerUpdater\updates", opts.DownloadDirectory);
        Assert.Equal("InstallerService", opts.TargetServiceName);
        Assert.Equal(3, opts.MaxRetries);
        Assert.Equal(@"SOFTWARE\rzwnz\InstallerService", opts.RegistryBasePath);
        Assert.Equal(120, opts.HttpTimeoutSeconds);
        Assert.True(opts.SilentInstall);
        Assert.Equal("InstallerUpdater", UpdaterOptions.SectionName);
    }

    [Fact]
    public void UpdateManifest_DefaultValues()
    {
        var manifest = new UpdateManifest();

        Assert.Equal(string.Empty, manifest.Version);
        Assert.Equal(string.Empty, manifest.DownloadUrl);
        Assert.Equal(string.Empty, manifest.Sha256Hash);
        Assert.Equal(0, manifest.FileSize);
        Assert.Null(manifest.ReleaseNotes);
        Assert.False(manifest.IsMandatory);
        Assert.Null(manifest.MinimumVersion);
    }

    [Fact]
    public void UpdateResult_DefaultValues()
    {
        var result = new UpdateResult();

        Assert.False(result.Success);
        Assert.Null(result.InstalledVersion);
        Assert.Null(result.PreviousVersion);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void UpdatePhase_AllValues_AreDefined()
    {
        var phases = Enum.GetValues<UpdatePhase>();
        Assert.Equal(9, phases.Length);
    }

    [Fact]
    public void UpdateManifest_CanSetAllProperties()
    {
        var now = DateTime.UtcNow;
        var manifest = new UpdateManifest
        {
            Version = "2.0.0",
            DownloadUrl = "http://server/update.exe",
            Sha256Hash = "abc123",
            FileSize = 1024 * 1024,
            ReleaseNotes = "Bug fixes",
            IsMandatory = true,
            MinimumVersion = "1.5.0",
            PublishedAt = now
        };

        Assert.Equal("2.0.0", manifest.Version);
        Assert.Equal("http://server/update.exe", manifest.DownloadUrl);
        Assert.Equal("abc123", manifest.Sha256Hash);
        Assert.Equal(1024 * 1024, manifest.FileSize);
        Assert.Equal("Bug fixes", manifest.ReleaseNotes);
        Assert.True(manifest.IsMandatory);
        Assert.Equal("1.5.0", manifest.MinimumVersion);
        Assert.Equal(now, manifest.PublishedAt);
    }
}
