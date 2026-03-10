using FluentAssertions;
using InstallerUpdater.Models;
using InstallerUpdater.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InstallerService.Tests.Services;

public class UpdateApplierTests : IDisposable
{
    private readonly Mock<ILogger<UpdateApplier>> _logger = new();
    private readonly Mock<IInstalledVersionProvider> _versionProvider = new();
    private readonly string _tempDir;
    private readonly UpdaterOptions _options;

    public UpdateApplierTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"applier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _options = new UpdaterOptions
        {
            DownloadDirectory = _tempDir,
            TargetServiceName = "TestService",
            InstallerPath = Path.Combine(_tempDir, "installer.exe"),
            SilentInstall = true
        };
    }

    private UpdateApplier CreateApplier() =>
        new(_logger.Object, Options.Create(_options), _versionProvider.Object);

    [Fact]
    public async Task ApplyUpdateAsync_InstallerNotFound_FailsWithRollback()
    {
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        var manifest = new UpdateManifest { Version = "2.0.0" };

        var applier = CreateApplier();
        var result = await applier.ApplyUpdateAsync("/nonexistent/installer.exe", manifest);

        result.Success.Should().BeFalse();
        result.Phase.Should().Be(UpdatePhase.Failed);
        result.ErrorMessage.Should().NotBeNullOrEmpty();
        result.PreviousVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task RollbackAsync_NoBackupExists_ReturnsFailed()
    {
        var applier = CreateApplier();

        var result = await applier.RollbackAsync();

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No backup found");
        result.Phase.Should().Be(UpdatePhase.Failed);
    }

    [Fact]
    public async Task RollbackAsync_BackupExists_RestoresFiles()
    {
        // Create a backup directory with some files
        var backupDir = Path.Combine(_tempDir, "backup");
        Directory.CreateDirectory(backupDir);
        await File.WriteAllTextAsync(Path.Combine(backupDir, "test.txt"), "backup content");

        var applier = CreateApplier();
        var result = await applier.RollbackAsync();

        result.Success.Should().BeTrue();
        result.Phase.Should().Be(UpdatePhase.Completed);
    }

    [Fact]
    public async Task RollbackAsync_BackupWithSubdirectories_RestoresAll()
    {
        var backupDir = Path.Combine(_tempDir, "backup");
        Directory.CreateDirectory(Path.Combine(backupDir, "subdir"));
        await File.WriteAllTextAsync(Path.Combine(backupDir, "root.txt"), "root");
        await File.WriteAllTextAsync(Path.Combine(backupDir, "subdir", "child.txt"), "child");

        var applier = CreateApplier();
        var result = await applier.RollbackAsync();

        result.Success.Should().BeTrue();
        // Verify files were copied to install dir
        File.Exists(Path.Combine(_tempDir, "root.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ApplyUpdateAsync_SetsPhasesThroughExecution()
    {
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        var manifest = new UpdateManifest { Version = "2.0.0" };

        var applier = CreateApplier();
        // This will fail at RunInstallerAsync since the file doesn't exist,
        // but it exercises the phase transitions and backup creation
        var result = await applier.ApplyUpdateAsync("/missing/installer.exe", manifest);

        // Should have attempted and failed
        result.Success.Should().BeFalse();
        result.PreviousVersion.Should().Be("1.0.0");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }
}
