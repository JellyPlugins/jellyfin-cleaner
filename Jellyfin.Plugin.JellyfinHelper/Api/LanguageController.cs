using System.Net.Mime;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
///     API controller exposing the plugin's resolved UI language so the dashboard can format
///     dates in the configured language rather than the browser locale.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("JellyfinHelper/Language")]
[Produces(MediaTypeNames.Application.Json)]
public class LanguageController : ControllerBase
{
    private readonly ICleanupConfigHelper _configHelper;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LanguageController" /> class.
    /// </summary>
    /// <param name="configHelper">The cleanup configuration helper.</param>
    public LanguageController(ICleanupConfigHelper configHelper)
    {
        _configHelper = configHelper;
    }

    /// <summary>
    ///     Gets the plugin's resolved UI language code.
    /// </summary>
    /// <returns>An object containing the resolved language code.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [AllowAnonymous] // Intentional: the locale is needed before user authentication, mirroring Translations.
    public ActionResult<LanguageResponse> GetLanguage()
    {
        var resolved = I18NService.ResolveLanguage(_configHelper.GetConfig().Language);
        return Ok(new LanguageResponse { Language = resolved });
    }
}
