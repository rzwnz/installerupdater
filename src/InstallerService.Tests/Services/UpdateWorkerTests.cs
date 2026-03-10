using FluentAssertions;
using InstallerUpdater.Models;
using InstallerUpdater.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InstallerService.Tests.Services;

public class UpdateWorkerTests
{
    private readonly Mock<ILogger<UpdateWorker>> _logger = new();
    private readonly Mock<IUpdateServerClient> _serverClient = new();
    private readonly Mock<IUpdateApplier> _applier = new();
    private readonly Mock<IInstalledVersionProvider> _versionProvider = new();
    private readonly Mock<IVersionComparer> _versionComparer = new();
    private readonly UpdaterOptions _options = new()
    {
        PollIntervalMinutes = 1,
        DownloadDirectory = "/tmp/update-worker-test"
    };

    private UpdateWorker CreateWorker() =>
        new(_logger.Object, _serverClient.Object, _applier.Object,
            _versionProvider.Object, _versionComparer.Object, Options.Create(_options));

    [Fact]
    public async Task CheckAndApplyUpdateAsync_NoManifest_DoesNothing()
    {
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateManifest?)null);

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _applier.Verify(a => a.ApplyUpdateAsync(
            It.IsAny<string>(), It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_UpToDate_DoesNotDownload()
    {
        var manifest = new UpdateManifest { Version = "1.0.0" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        _versionComparer.Setup(v => v.IsNewer("1.0.0", "1.0.0")).Returns(false);

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _serverClient.Verify(s => s.DownloadUpdateAsync(
            It.IsAny<UpdateManifest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_BelowMinimumVersion_DoesNotApply()
    {
        var manifest = new UpdateManifest { Version = "2.0.0", MinimumVersion = "1.5.0" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        _versionComparer.Setup(v => v.IsNewer("2.0.0", "1.0.0")).Returns(true);
        _versionComparer.Setup(v => v.IsAtLeast("1.0.0", "1.5.0")).Returns(false);

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _applier.Verify(a => a.ApplyUpdateAsync(
            It.IsAny<string>(), It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_UpdateAvailable_DownloadsAndApplies()
    {
        var manifest = new UpdateManifest { Version = "2.0.0", DownloadUrl = "http://x/update.exe" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        _versionComparer.Setup(v => v.IsNewer("2.0.0", "1.0.0")).Returns(true);
        _serverClient.Setup(s => s.DownloadUpdateAsync(
                manifest, _options.DownloadDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/updates/setup.exe");
        _applier.Setup(a => a.ApplyUpdateAsync(
                "/tmp/updates/setup.exe", manifest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult
            {
                Success = true, PreviousVersion = "1.0.0", InstalledVersion = "2.0.0"
            });

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _serverClient.Verify(s => s.DownloadUpdateAsync(
            manifest, _options.DownloadDirectory, It.IsAny<CancellationToken>()), Times.Once);
        _applier.Verify(a => a.ApplyUpdateAsync(
            "/tmp/updates/setup.exe", manifest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_ApplyFails_LogsError()
    {
        var manifest = new UpdateManifest { Version = "2.0.0", DownloadUrl = "http://x/update.exe" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.0.0");
        _versionComparer.Setup(v => v.IsNewer("2.0.0", "1.0.0")).Returns(true);
        _serverClient.Setup(s => s.DownloadUpdateAsync(
                manifest, _options.DownloadDirectory, It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/updates/setup.exe");
        _applier.Setup(a => a.ApplyUpdateAsync(
                "/tmp/updates/setup.exe", manifest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult
            {
                Success = false, Phase = UpdatePhase.ApplyingUpdate, ErrorMessage = "install failed"
            });

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        // Should still call applier, just logs the failure
        _applier.Verify(a => a.ApplyUpdateAsync(
            It.IsAny<string>(), manifest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_NullCurrentVersion_DefaultsToZero()
    {
        var manifest = new UpdateManifest { Version = "1.0.0" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns((string?)null);
        _versionComparer.Setup(v => v.IsNewer("1.0.0", "0.0.0")).Returns(true);
        _serverClient.Setup(s => s.DownloadUpdateAsync(
                It.IsAny<UpdateManifest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/updates/setup.exe");
        _applier.Setup(a => a.ApplyUpdateAsync(
                It.IsAny<string>(), It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult { Success = true });

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _versionComparer.Verify(v => v.IsNewer("1.0.0", "0.0.0"), Times.Once);
    }

    [Fact]
    public async Task CheckAndApplyUpdateAsync_MeetsMinimumVersion_Applies()
    {
        var manifest = new UpdateManifest { Version = "2.0.0", MinimumVersion = "1.0.0" };
        _serverClient.Setup(s => s.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manifest);
        _versionProvider.Setup(v => v.GetInstalledVersion()).Returns("1.2.0");
        _versionComparer.Setup(v => v.IsNewer("2.0.0", "1.2.0")).Returns(true);
        _versionComparer.Setup(v => v.IsAtLeast("1.2.0", "1.0.0")).Returns(true);
        _serverClient.Setup(s => s.DownloadUpdateAsync(
                It.IsAny<UpdateManifest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/tmp/updates/setup.exe");
        _applier.Setup(a => a.ApplyUpdateAsync(
                It.IsAny<string>(), It.IsAny<UpdateManifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UpdateResult { Success = true });

        var worker = CreateWorker();
        await worker.CheckAndApplyUpdateAsync(CancellationToken.None);

        _applier.Verify(a => a.ApplyUpdateAsync(
            It.IsAny<string>(), manifest, It.IsAny<CancellationToken>()), Times.Once);
    }
}
