using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using InstallerService.Models;
using InstallerService.Services;

namespace InstallerService.Tests.Services;

public class TomcatClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly TomcatClient _sut;

    public TomcatClientTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var httpClient = new HttpClient(_handlerMock.Object);
        var logger = NullLoggerFactory.Instance.CreateLogger<TomcatClient>();
        var options = Options.Create(new InstallerServiceOptions
        {
            TomcatBaseUrl = "http://localhost:8080"
        });
        _sut = new TomcatClient(httpClient, logger, options);
    }

    [Fact]
    public async Task HeartbeatAsync_ServerUp_ReturnsAlive()
    {
        SetupHandler(HttpStatusCode.OK, "OK");

        var result = await _sut.HeartbeatAsync();

        Assert.True(result.IsAlive);
        Assert.Equal(200, result.StatusCode);
    }

    [Fact]
    public async Task HeartbeatAsync_ServerDown_ReturnsNotAlive()
    {
        SetupHandler(HttpStatusCode.ServiceUnavailable, "Down");

        var result = await _sut.HeartbeatAsync();

        Assert.False(result.IsAlive);
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task HeartbeatAsync_Exception_ReturnsNotAlive()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _sut.HeartbeatAsync();

        Assert.False(result.IsAlive);
        Assert.Equal(0, result.StatusCode);
        Assert.Contains("Connection refused", result.Message);
    }

    [Fact]
    public async Task ValidateAuthAsync_SuccessResponse_ReturnsTrue()
    {
        SetupHandler(HttpStatusCode.OK, "{}");

        var result = await _sut.ValidateAuthAsync("user1", "TANDEM");

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAuthAsync_Unauthorized_ReturnsFalse()
    {
        SetupHandler(HttpStatusCode.Unauthorized, "Unauthorized");

        var result = await _sut.ValidateAuthAsync("baduser", "TANDEM");

        Assert.False(result);
    }

    [Fact]
    public async Task ValidateAuthAsync_Exception_ReturnsFalse()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var result = await _sut.ValidateAuthAsync("user", "DOMAIN");

        Assert.False(result);
    }

    [Fact]
    public async Task FlashMessageAsync_Success_ReturnsTrue()
    {
        SetupHandler(HttpStatusCode.OK, "{}");

        var result = await _sut.FlashMessageAsync("Test message", "info");

        Assert.True(result);
    }

    [Fact]
    public async Task FlashMessageAsync_ServerError_ReturnsFalse()
    {
        SetupHandler(HttpStatusCode.InternalServerError, "Error");

        var result = await _sut.FlashMessageAsync("Test", "error");

        Assert.False(result);
    }

    [Fact]
    public async Task FlashMessageAsync_Exception_ReturnsFalse()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        var result = await _sut.FlashMessageAsync("msg", "warn");

        Assert.False(result);
    }

    [Fact]
    public async Task ReportStatusAsync_DoesNotThrow_OnError()
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Unreachable"));

        // Should not throw
        await _sut.ReportStatusAsync("Running", "All good");
    }

    [Fact]
    public async Task HeartbeatAsync_Timestamp_IsRecentUtc()
    {
        SetupHandler(HttpStatusCode.OK, "OK");
        var before = DateTime.UtcNow;

        var result = await _sut.HeartbeatAsync();

        Assert.InRange(result.Timestamp, before, DateTime.UtcNow.AddSeconds(1));
    }

    private void SetupHandler(HttpStatusCode statusCode, string content)
    {
        _handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
