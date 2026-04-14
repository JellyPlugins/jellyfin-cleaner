using System.Net.Mime;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Jellyfin.Plugin.JellyfinHelper.Services.PluginLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
/// API controller for settings.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("JellyfinHelper/Configuration")]
[Produces(MediaTypeNames.Application.Json)]
public class ConfigurationController : ControllerBase
{
    private readonly ILogger<ConfigurationController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationController"/> class.
    /// </summary>
    /// <param name="logger">The controller logger.</param>
    public ConfigurationController(ILogger<ConfigurationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the current plugin configuration.
    /// </summary>
    /// <returns>The plugin configuration.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<PluginConfiguration> GetConfiguration()
    {
        var config = CleanupConfigHelper.GetConfig();
        return Ok(config);
    }

    /// <summary>
    /// Updates the plugin configuration.
    /// </summary>
    /// <param name="request">The configuration update request.</param>
    /// <returns>A status result.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult UpdateConfiguration([FromBody] ConfigurationUpdateRequest request)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
        {
            return BadRequest(new { message = "Plugin not initialized." });
        }

        // Validate
        if (request.OrphanMinAgeDays < 0)
        {
            return BadRequest(new { message = "OrphanMinAgeDays must be >= 0." });
        }

        if (request.TrashRetentionDays < 0)
        {
            return BadRequest(new { message = "TrashRetentionDays must be >= 0." });
        }

        // Apply request values to the existing config (preserves accumulated statistics and internal state)
        var config = plugin.Configuration;

        config.IncludedLibraries = request.IncludedLibraries;
        config.ExcludedLibraries = request.ExcludedLibraries;
        config.OrphanMinAgeDays = request.OrphanMinAgeDays;

        config.TrickplayTaskMode = request.TrickplayTaskMode;
        config.EmptyMediaFolderTaskMode = request.EmptyMediaFolderTaskMode;
        config.OrphanedSubtitleTaskMode = request.OrphanedSubtitleTaskMode;
        config.StrmRepairTaskMode = request.StrmRepairTaskMode;

        config.UseTrash = request.UseTrash;
        config.TrashFolderPath = request.TrashFolderPath;
        config.TrashRetentionDays = request.TrashRetentionDays;

        config.RadarrUrl = request.RadarrUrl;
        config.RadarrApiKey = request.RadarrApiKey;
        config.SonarrUrl = request.SonarrUrl;
        config.SonarrApiKey = request.SonarrApiKey;

        config.Language = request.Language;

        // Update Radarr instances (clear + re-add from request)
        config.RadarrInstances.Clear();
        foreach (var instance in request.RadarrInstances)
        {
            config.RadarrInstances.Add(instance);
        }

        // Update Sonarr instances (clear + re-add from request)
        config.SonarrInstances.Clear();
        foreach (var instance in request.SonarrInstances)
        {
            config.SonarrInstances.Add(instance);
        }

        plugin.SaveConfiguration();

        PluginLogService.LogInfo("API", "Plugin configuration updated.", _logger);
        return Ok(new { message = "Configuration saved." });
    }
}