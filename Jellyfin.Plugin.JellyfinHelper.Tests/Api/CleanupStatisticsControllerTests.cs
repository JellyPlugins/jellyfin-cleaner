using System.Text.Json;
using Jellyfin.Plugin.JellyfinHelper.Api;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Jellyfin.Plugin.JellyfinHelper.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Api;

[Collection("ConfigOverride")]
public class CleanupStatisticsControllerTests
{
    private readonly CleanupStatisticsController _controller;

    public CleanupStatisticsControllerTests()
    {
        ControllerTestFactory.InitializePluginInstance();
        var trackingServiceMock = new Mock<ICleanupTrackingService>();
        trackingServiceMock.Setup(t => t.GetStatistics())
            .Returns((0L, 0, DateTime.MinValue));
        _controller = new CleanupStatisticsController(trackingServiceMock.Object);
    }

    [Fact]
    public void GetCleanupStatistics_ReturnsOk()
    {
        var result = _controller.GetCleanupStatistics();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var payloadJson = JsonSerializer.Serialize(okResult.Value);
        Assert.Contains("TotalBytesFreed", payloadJson);
        Assert.Contains("TotalItemsDeleted", payloadJson);
        Assert.Contains("LastCleanupTimestamp", payloadJson);
    }
}
