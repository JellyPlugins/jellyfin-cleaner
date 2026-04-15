using Jellyfin.Plugin.JellyfinHelper.Api;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Jellyfin.Plugin.JellyfinHelper.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests.Api;

[Collection("ConfigOverride")]
public class TranslationsControllerTests
{
    private readonly Mock<ICleanupConfigHelper> _configHelperMock;
    private readonly TranslationsController _controller;

    public TranslationsControllerTests()
    {
        ControllerTestFactory.InitializePluginInstance();
        _configHelperMock = new Mock<ICleanupConfigHelper>();
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration());
        _controller = new TranslationsController(_configHelperMock.Object);
    }

    [Fact]
    public void GetTranslations_ReturnsTranslations()
    {
        var result = _controller.GetTranslations("en");

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var translations = Assert.IsType<Dictionary<string, string>>(okResult.Value);
        Assert.NotEmpty(translations);
    }

    [Fact]
    public void GetTranslations_DefaultsToConfigLanguage()
    {
        _configHelperMock.Setup(c => c.GetConfig()).Returns(new PluginConfiguration { Language = "de" });

        var result = _controller.GetTranslations();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var translations = Assert.IsType<Dictionary<string, string>>(okResult.Value);
        Assert.NotEmpty(translations);
        Assert.Equal("Einstellungen", translations["tabSettings"]);
    }
}