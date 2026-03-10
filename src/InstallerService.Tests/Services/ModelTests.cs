using InstallerService.Models;

namespace InstallerService.Tests.Services;

public class ModelTests
{
    [Fact]
    public void InstallerServiceOptions_DefaultValues_AreCorrect()
    {
        var opts = new InstallerServiceOptions();

        Assert.Equal("http://localhost:8080", opts.TomcatBaseUrl);
        Assert.Equal(30, opts.HeartbeatIntervalSeconds);
        Assert.Equal(string.Empty, opts.AdDomain);
        Assert.Equal(InstallerServiceOptions.DatabaseProviderSqlite, opts.DatabaseProvider);
        Assert.Equal(@"C:\ProgramData\InstallerUpdater\installer.db", opts.LocalDbPath);
        Assert.Equal(string.Empty, opts.PostgresConnectionString);
        Assert.Equal(@"C:\ProgramData\InstallerUpdater", opts.WorkingDirectory);
        Assert.Equal(@"SOFTWARE\rzwnz\InstallerService", opts.RegistryBasePath);
    }

    [Fact]
    public void HeartbeatResult_DefaultTimestamp_IsUtcNow()
    {
        var before = DateTime.UtcNow;
        var result = new HeartbeatResult();
        Assert.InRange(result.Timestamp, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void HeartbeatResult_Properties_CanBeSet()
    {
        var result = new HeartbeatResult
        {
            IsAlive = true,
            Message = "OK",
            StatusCode = 200,
            Timestamp = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        Assert.True(result.IsAlive);
        Assert.Equal("OK", result.Message);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), result.Timestamp);
    }

    [Fact]
    public void MigrationRecord_DefaultValues()
    {
        var record = new MigrationRecord();

        Assert.Equal(0, record.Id);
        Assert.Equal(string.Empty, record.Version);
        Assert.Equal(string.Empty, record.Description);
        Assert.Equal(string.Empty, record.Script);
        Assert.False(record.Success);
        Assert.Null(record.ErrorMessage);
    }

    [Fact]
    public void RegistryEntry_DefaultValues()
    {
        var entry = new RegistryEntry();

        Assert.Equal(string.Empty, entry.KeyPath);
        Assert.Equal(string.Empty, entry.ValueName);
        Assert.Null(entry.Value);
        Assert.Equal(RegistryValueKind.String, entry.Kind);
    }

    [Fact]
    public void RegistryValueKind_AllValues_AreDefined()
    {
        var values = Enum.GetValues<RegistryValueKind>();
        Assert.Equal(6, values.Length);
        Assert.Contains(RegistryValueKind.String, values);
        Assert.Contains(RegistryValueKind.DWord, values);
        Assert.Contains(RegistryValueKind.QWord, values);
        Assert.Contains(RegistryValueKind.Binary, values);
        Assert.Contains(RegistryValueKind.MultiString, values);
        Assert.Contains(RegistryValueKind.ExpandString, values);
    }

    [Fact]
    public void InstallerServiceOptions_SectionName_IsCorrect()
    {
        Assert.Equal("InstallerService", InstallerServiceOptions.SectionName);
    }
}
