using FluentAssertions;
using InstallerService.Configuration;
using InstallerService.Database;
using InstallerService.Models;
using InstallerService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InstallerService.Tests.Services;

public class InstallerWorkerTests
{
    private readonly Mock<ILogger<InstallerWorker>> _logger = new();
    private readonly Mock<ITomcatClient> _tomcatClient = new();
    private readonly Mock<IRegistryManager> _registryManager = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly Mock<IDatabaseMigrator> _migrator = new();
    private readonly InstallerServiceOptions _options = new()
    {
        WorkingDirectory = "/tmp/installer-worker-test",
        RegistryBasePath = @"SOFTWARE\Test\InstallerWorker",
        HeartbeatIntervalSeconds = 1
    };

    public InstallerWorkerTests()
    {
        // Default mocks so ExecuteAsync background thread doesn't fail
        _migrator.Setup(m => m.MigrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MigrationRecord>());
        _tomcatClient.Setup(t => t.ReportStatusAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _tomcatClient.Setup(t => t.HeartbeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeartbeatResult { IsAlive = true, Timestamp = DateTime.UtcNow });
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(false);
    }

    private InstallerWorker CreateWorker() =>
        new(_logger.Object, _tomcatClient.Object, _registryManager.Object,
            _fileSystem.Object, _migrator.Object, Options.Create(_options));

    [Fact]
    public async Task StartAsync_EnsuresDirectoryAndWritesRegistry()
    {
        var worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        _fileSystem.Verify(f => f.EnsureDirectory(_options.WorkingDirectory), Times.Once);
        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "Version", It.IsAny<string>()), Times.Once);
        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "InstallPath", _options.WorkingDirectory), Times.Once);
        _tomcatClient.Verify(t =>
            t.ReportStatusAsync("Starting", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StartAsync_AppliedMigrations_WritesDbVersion()
    {
        var migrations = new List<MigrationRecord> { new() { Version = "1.0" } };
        _migrator.Setup(m => m.MigrateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(migrations);
        _migrator.Setup(m => m.GetCurrentVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("1.0");

        var worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "DbVersion", "1.0"), Times.Once);
    }

    [Fact]
    public async Task StartAsync_MigrationException_FlashesErrorAndContinues()
    {
        _migrator.Setup(m => m.MigrateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new MigrationException("migration failed"));
        _tomcatClient.Setup(t => t.FlashMessageAsync(
                It.IsAny<string>(), "error", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var worker = CreateWorker();
        // StartAsync should NOT throw — it catches MigrationException
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        _tomcatClient.Verify(t => t.FlashMessageAsync(
            It.Is<string>(s => s.Contains("migration failed")),
            "error", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatAlive_SetsOKStatus()
    {
        using var cts = new CancellationTokenSource();
        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);

        // Wait for at least one heartbeat cycle
        await Task.Delay(1500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "TomcatStatus", "OK"), Times.AtLeastOnce);
        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "LastHeartbeat", It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatNotAlive_SetsUnreachable()
    {
        _tomcatClient.Setup(t => t.HeartbeatAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HeartbeatResult
            {
                IsAlive = false,
                Message = "Connection refused",
                Timestamp = DateTime.UtcNow
            });

        using var cts = new CancellationTokenSource();
        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(1500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "TomcatStatus", "Unreachable"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_LogDirectoryExists_CleansOldFiles()
    {
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.CleanOldFiles(It.IsAny<string>(), It.IsAny<TimeSpan>(), "*.log"))
            .Returns(0);

        using var cts = new CancellationTokenSource();
        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(1500);
        await cts.CancelAsync();
        await worker.StopAsync(CancellationToken.None);

        _fileSystem.Verify(f =>
            f.CleanOldFiles(It.IsAny<string>(), TimeSpan.FromDays(30), "*.log"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatThrows_ContinuesRunning()
    {
        _tomcatClient.Setup(t => t.HeartbeatAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("network error"));

        using var cts = new CancellationTokenSource();
        var worker = CreateWorker();
        await worker.StartAsync(cts.Token);

        await Task.Delay(1500);
        await cts.CancelAsync();

        // Worker should not throw — it catches generic exceptions
        await worker.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_WritesStoppedStatusAndReportsToTomcat()
    {
        var worker = CreateWorker();
        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        _registryManager.Verify(r =>
            r.WriteString(_options.RegistryBasePath, "Status", "Stopped"), Times.Once);
        _tomcatClient.Verify(t =>
            t.ReportStatusAsync("Stopped", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
