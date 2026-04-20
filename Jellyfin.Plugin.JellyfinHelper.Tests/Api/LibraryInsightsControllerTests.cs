using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyfinHelper.Api;
using Jellyfin.Plugin.JellyfinHelper.Services.Timeline;
using Jellyfin.Plugin.JellyfinHelper.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Api;

public class LibraryInsightsControllerTests
{
    private readonly IMemoryCache _cache;
    private readonly LibraryInsightsController _controller;
    private readonly Mock<ILibraryInsightsService> _serviceMock;

    public LibraryInsightsControllerTests()
    {
        _cache = TestMockFactory.CreateMemoryCache();
        _serviceMock = TestMockFactory.CreateLibraryInsightsService();
        _controller = new LibraryInsightsController(_cache, _serviceMock.Object);
    }

    [Fact]
    public async Task GetInsightsAsync_ReturnsComputedResult()
    {
        var expected = new LibraryInsightsResult { RecentTotalCount = 42 };
        _serviceMock
            .Setup(s => s.ComputeInsightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetInsightsAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var data = Assert.IsType<LibraryInsightsResult>(okResult.Value);
        Assert.Equal(42, data.RecentTotalCount);
    }

    [Fact]
    public async Task GetInsightsAsync_ReturnsCachedResult_OnSecondCall()
    {
        var expected = new LibraryInsightsResult { RecentTotalCount = 10 };
        _serviceMock
            .Setup(s => s.ComputeInsightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // First call — computes and caches
        await _controller.GetInsightsAsync(CancellationToken.None);

        // Second call — should return cached result without calling service again
        var result = await _controller.GetInsightsAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var data = Assert.IsType<LibraryInsightsResult>(okResult.Value);
        Assert.Equal(10, data.RecentTotalCount);
        _serviceMock.Verify(s => s.ComputeInsightsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetInsightsAsync_CallsServiceOnCacheExpiry()
    {
        var first = new LibraryInsightsResult { RecentTotalCount = 1 };
        var second = new LibraryInsightsResult { RecentTotalCount = 2 };
        var callCount = 0;
        _serviceMock
            .Setup(s => s.ComputeInsightsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++callCount == 1 ? first : second);

        // First call — computes
        await _controller.GetInsightsAsync(CancellationToken.None);

        // Evict cache manually to simulate expiry
        _cache.Remove(LibraryInsightsController.InsightsCacheKey);

        // Second call — should compute again
        var result = await _controller.GetInsightsAsync(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var data = Assert.IsType<LibraryInsightsResult>(okResult.Value);
        Assert.Equal(2, data.RecentTotalCount);
        _serviceMock.Verify(s => s.ComputeInsightsAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}