using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.JellyfinHelper;

/// <summary>
/// The main plugin.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Jellyfin Helper";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse("0c737645-5cbb-4bd8-80c7-d377b560aaa4");

    /// <inheritdoc />
    public override string? Description => "Automated cleanup (trickplay, empty folders, subtitles), media statistics, trash bin, Arr integration.";

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the plugin thumb image stream from the embedded resource.
    /// </summary>
    /// <returns>The image stream, or null if the resource is not found.</returns>
    public Stream? GetThumbImage()
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(
            GetType().Namespace + ".logo.png");
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                DisplayName = "Jellyfin Helper",
                EnableInMainMenu = true,
                MenuIcon = "handyman",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            }
        ];
    }
}