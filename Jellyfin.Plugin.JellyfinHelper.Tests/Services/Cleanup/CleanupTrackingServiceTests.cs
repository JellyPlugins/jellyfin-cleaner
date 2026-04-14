using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Services.Cleanup;

/// <summary>
/// Tests for <see cref="CleanupTrackingService"/>.
/// Note: Since Plugin.Instance requires a full plugin setup, these tests cover
/// the null-instance fallback paths. The tracking logic itself is straightforward
/// (increment + save), so the null-safety is the critical path to verify.
/// </summary>
[Collection("ConfigOverride")]
public class CleanupTrackingServiceTests : IDisposable
{
    private readonly Mock<ILogger> _loggerMock = new();

    public CleanupTrackingServiceTests()
    {
        // Use an explicit override to isolate these tests from the global Plugin.Instance.
        CleanupConfigHelper.ConfigOverride = new PluginConfiguration();
    }

    public void Dispose()
    {
        CleanupConfigHelper.ConfigOverride = null;
    }

    [Fact]
    public void GetStatistics_WhenPluginInstanceNull_ReturnsDefaults()
    {
        // GetStatistics now respects CleanupConfigHelper.ConfigOverride
        var (totalBytesFreed, totalItemsDeleted, lastCleanupTimestamp) = CleanupTrackingService.GetStatistics();

        Assert.Equal(0, totalBytesFreed);
        Assert.Equal(0, totalItemsDeleted);
        Assert.Equal(DateTime.MinValue, lastCleanupTimestamp);
    }

    [Fact]
    public void RecordCleanup_WhenPluginInstanceNull_DoesNotThrow()
    {
        // Should use ConfigOverride and not throw
        var exception = Record.Exception(() =>
            CleanupTrackingService.RecordCleanup(1024, 5, _loggerMock.Object));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordCleanup_WhenPluginInstanceNull_StatisticsAreUpdated()
    {
        // RecordCleanup updates the ConfigOverride if it is set.
        // We want to verify that when we record, statistics ARE updated in our controlled config.
        CleanupTrackingService.RecordCleanup(2048, 10, _loggerMock.Object);

        var (totalBytesFreed, totalItemsDeleted, lastCleanupTimestamp) = CleanupTrackingService.GetStatistics();

        Assert.Equal(2048, totalBytesFreed);
        Assert.Equal(10, totalItemsDeleted);
        Assert.NotEqual(DateTime.MinValue, lastCleanupTimestamp);
    }

    [Fact]
    public void RecordCleanup_WithZeroValues_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            CleanupTrackingService.RecordCleanup(0, 0, _loggerMock.Object));

        Assert.Null(exception);
    }

    [Fact]
    public void RecordCleanup_WithNegativeValues_DoesNotThrow()
    {
        // Edge case: negative values should not crash the service
        var exception = Record.Exception(() =>
            CleanupTrackingService.RecordCleanup(-100, -1, _loggerMock.Object));

        Assert.Null(exception);
    }
}
