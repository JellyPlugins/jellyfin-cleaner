using System.Collections.Generic;
using System.Net.Mime;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Jellyfin.Plugin.JellyfinHelper.Services.Cleanup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.JellyfinHelper.Api;

/// <summary>
/// API controller for I18n Translations.
/// </summary>
[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("JellyfinHelper/Translations")]
[Produces(MediaTypeNames.Application.Json)]
public class TranslationsController : ControllerBase
{
    private readonly ICleanupConfigHelper _configHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationsController"/> class.
    /// </summary>
    /// <param name="configHelper">The cleanup configuration helper.</param>
    public TranslationsController(ICleanupConfigHelper configHelper)
    {
        _configHelper = configHelper;
    }

    /// <summary>
    /// Gets the translation strings for the specified language (or the configured language).
    /// </summary>
    /// <param name="lang">Optional language code override. If not provided, uses the configured language.</param>
    /// <returns>A dictionary of translation keys to strings.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [AllowAnonymous] // Intentional: translations are needed before user authentication (e.g. login page)
    public ActionResult<Dictionary<string, string>> GetTranslations([FromQuery] string? lang = null)
    {
        var languageCode = string.IsNullOrWhiteSpace(lang) ? _configHelper.GetConfig().Language : lang.Trim();
        var translations = I18nService.GetTranslations(languageCode);
        return Ok(translations);
    }
}
