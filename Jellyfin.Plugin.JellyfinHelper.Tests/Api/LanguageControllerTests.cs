using Jellyfin.Plugin.JellyfinHelper.Api;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Jellyfin.Plugin.JellyfinHelper.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Api;

[Collection("ConfigOverride")]
public class LanguageControllerTests
{
    private readonly Mock<ICleanupConfigHelper> _configHelperMock;
    private readonly LanguageController _controller;

    public LanguageControllerTests()
    {
        ControllerTestFactory.InitializePluginInstance();
        _configHelperMock = new Mock<ICleanupConfigHelper>();
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration());
        _controller = new LanguageController(_configHelperMock.Object);
    }

    [Fact]
    public void GetLanguage_ReturnsConfiguredLanguage()
    {
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration { Language = "de" });

        var result = _controller.GetLanguage();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LanguageResponse>(okResult.Value);
        Assert.Equal("de", body.Language);
    }

    [Fact]
    public void GetLanguage_DefaultConfig_ReturnsEnglish()
    {
        var result = _controller.GetLanguage();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LanguageResponse>(okResult.Value);
        Assert.Equal("en", body.Language);
    }

    [Theory]
    [InlineData("de-DE", "de")]
    [InlineData("pt_BR", "pt")]
    [InlineData("SV", "sv")]
    public void GetLanguage_NormalizesToSupportedPrimarySubtag(string configured, string expected)
    {
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration { Language = configured });

        var result = _controller.GetLanguage();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LanguageResponse>(okResult.Value);
        Assert.Equal(expected, body.Language);
    }

    [Fact]
    public void GetLanguage_UnknownConfiguredLanguage_FallsBackToEnglish()
    {
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration { Language = "klingon" });

        var result = _controller.GetLanguage();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<LanguageResponse>(okResult.Value);
        Assert.Equal("en", body.Language);
    }
}
