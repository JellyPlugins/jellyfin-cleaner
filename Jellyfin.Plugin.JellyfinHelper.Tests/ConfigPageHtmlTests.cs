using System.Reflection;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellyfinHelper.Configuration;
using Jellyfin.Plugin.JellyfinHelper.Services;
using Xunit;

namespace Jellyfin.Plugin.JellyfinHelper.Tests;

/// <summary>
/// Structural tests for the configPage.html embedded resource.
/// Validates that the HTML configuration page contains the expected elements,
/// IDs, and TaskMode options that match the PluginConfiguration properties.
/// </summary>
public class ConfigPageHtmlTests
{
    private static readonly string HtmlContent = LoadConfigPageHtml();
    private static readonly string ReadmeContent = LoadReadme();

    private static string LoadConfigPageHtml()
    {
        var assembly = typeof(PluginConfiguration).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("configPage.html", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(resourceName);

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string LoadReadme()
    {
        // Walk up from bin/Debug/net9.0 to the repository root
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "README.md");
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            dir = Path.GetDirectoryName(dir);
        }

        return string.Empty;
    }

    // ── renderTaskModeSelect function ──────────────────────────────────

    [Fact]
    public void Html_ContainsRenderTaskModeSelectFunction()
    {
        Assert.Contains("function renderTaskModeSelect", HtmlContent);
    }

    [Fact]
    public void Html_RenderTaskModeSelect_ContainsAllThreeOptions()
    {
        // The function should generate options for Deactivate, DryRun, Activate
        Assert.Contains("Deactivate", HtmlContent);
        Assert.Contains("DryRun", HtmlContent);
        Assert.Contains("Activate", HtmlContent);
    }

    // ── TaskMode select elements for each sub-task ─────────────────────

    [Theory]
    [InlineData("cfgTrickplayMode", "TrickplayTaskMode")]
    [InlineData("cfgEmptyFolderMode", "EmptyMediaFolderTaskMode")]
    [InlineData("cfgSubtitleMode", "OrphanedSubtitleTaskMode")]
    [InlineData("cfgStrmMode", "StrmRepairTaskMode")]
    public void Html_ContainsSelectElementForTaskMode(string elementId, string configProperty)
    {
        // Verify the select element is rendered with the correct ID
        Assert.Contains($"'{elementId}'", HtmlContent);

        // Verify the config property is read when loading
        Assert.Contains($"cfg.{configProperty}", HtmlContent);
    }

    [Theory]
    [InlineData("cfgTrickplayMode", "TrickplayTaskMode")]
    [InlineData("cfgEmptyFolderMode", "EmptyMediaFolderTaskMode")]
    [InlineData("cfgSubtitleMode", "OrphanedSubtitleTaskMode")]
    [InlineData("cfgStrmMode", "StrmRepairTaskMode")]
    public void Html_SavesTaskModeFromSelectElement(string elementId, string configProperty)
    {
        // Verify the config property is written when saving
        var savePattern = $"{configProperty}: document.getElementById('{elementId}').value";
        Assert.Contains(savePattern, HtmlContent);
    }

    [Theory]
    [InlineData("cfgTrickplayMode")]
    [InlineData("cfgEmptyFolderMode")]
    [InlineData("cfgSubtitleMode")]
    [InlineData("cfgStrmMode")]
    public void Html_DefaultsToDryRunWhenConfigPropertyMissing(string elementId)
    {
        // Each renderTaskModeSelect call should have a fallback: || 'DryRun'
        var pattern = new Regex($@"renderTaskModeSelect\s*\(\s*'{Regex.Escape(elementId)}'.*\|\|\s*'DryRun'\s*\)");
        Assert.Matches(pattern, HtmlContent);
    }

    // ── No legacy checkbox elements for dry-run ────────────────────────

    [Fact]
    public void Html_DoesNotContainLegacyDryRunCheckboxes()
    {
        // Old checkbox IDs that should no longer exist
        Assert.DoesNotContain("cfgDryRunTrickplay", HtmlContent);
        Assert.DoesNotContain("cfgDryRunEmptyFolders", HtmlContent);
        Assert.DoesNotContain("cfgDryRunSubtitles", HtmlContent);
        Assert.DoesNotContain("cfgDryRunStrm", HtmlContent);
    }

    // ── Task labels are present ────────────────────────────────────────

    [Theory]
    [InlineData("Trickplay Folder Cleaner")]
    [InlineData("Empty Media Folder Cleaner")]
    [InlineData("Orphaned Subtitle Cleaner")]
    [InlineData(".strm File Repair")]
    public void Html_ContainsTaskLabel(string label)
    {
        Assert.Contains(label, HtmlContent);
    }

    // ── General page structure ─────────────────────────────────────────

    [Fact]
    public void Html_ContainsPluginConfigPageClass()
    {
        Assert.Contains("pluginConfigurationPage", HtmlContent);
    }

    [Fact]
    public void Html_ContainsJellyfinHelperPageId()
    {
        Assert.Contains("JellyfinHelperConfigPage", HtmlContent);
    }

    [Fact]
    public void Html_ContainsSaveButton()
    {
        // There should be a save/submit mechanism
        Assert.Matches(new Regex(@"(save|submit|btnSave)", RegexOptions.IgnoreCase), HtmlContent);
    }

    [Fact]
    public void Html_TaskModeEnumValues_MatchSelectOptions()
    {
        // Verify all TaskMode enum values are represented in the HTML
        foreach (var value in Enum.GetNames<TaskMode>())
        {
            Assert.Contains(value, HtmlContent);
        }
    }

    [Fact]
    public void Html_AllPluginConfigTaskModeProperties_HaveCorrespondingSelect()
    {
        // Use reflection to find all TaskMode properties on PluginConfiguration
        var taskModeProperties = typeof(PluginConfiguration)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(TaskMode))
            .Select(p => p.Name)
            .ToList();

        Assert.NotEmpty(taskModeProperties);

        foreach (var prop in taskModeProperties)
        {
            // Each TaskMode property should be referenced in the HTML (cfg.PropertyName)
            Assert.Contains($"cfg.{prop}", HtmlContent);

            // Each TaskMode property should appear in the save logic (PropertyName:)
            Assert.Contains($"{prop}:", HtmlContent);
        }
    }

    // ── Trends tab: JS references correct StatisticsSnapshot properties ──

    [Fact]
    public void Html_TrendChart_AllReferencedSnapshotProperties_ExistOnClass()
    {
        var snapshotProperties = typeof(StatisticsSnapshot)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Scope to renderTrendChart function and extract snapshots[*].Property references.
        var fnMatch = Regex.Match(
            HtmlContent,
            @"function\s+renderTrendChart\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n\s{4}\}",
            RegexOptions.Multiline);
        Assert.True(fnMatch.Success, "renderTrendChart function not found.");

        var referenced = Regex.Matches(fnMatch.Groups["body"].Value, @"snapshots\[[^\]]+\]\.(\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(referenced);

        foreach (var prop in referenced)
        {
            Assert.Contains(prop, snapshotProperties);
        }
    }

    // ── README.md quality checks ─────────────────────────────────────────

    [Fact]
    public void Readme_IsNotEmpty()
    {
        Assert.False(string.IsNullOrWhiteSpace(ReadmeContent), "README.md could not be loaded or is empty.");
    }

    [Fact]
    public void Readme_DoesNotContainObsoleteAlignAttribute()
    {
        // The HTML align= attribute is obsolete; use CSS or Markdown syntax instead
        var pattern = new Regex(@"<\w+[^>]*\balign\s*=", RegexOptions.IgnoreCase);
        Assert.DoesNotMatch(pattern, ReadmeContent);
    }

    [Theory]
    [InlineData("bgcolor")]
    [InlineData("valign")]
    [InlineData("cellpadding")]
    [InlineData("cellspacing")]
    [InlineData("border")]
    public void Readme_DoesNotContainObsoleteHtmlAttribute(string attribute)
    {
        var pattern = new Regex($@"<\w+[^>]*\b{attribute}\s*=", RegexOptions.IgnoreCase);
        Assert.DoesNotMatch(pattern, ReadmeContent);
    }

    [Fact]
    public void Readme_DoesNotContainDeprecatedHtmlTags()
    {
        // Deprecated tags: <center>, <font>, <marquee>, <blink>
        var pattern = new Regex(@"<\s*/?\s*(center|font|marquee|blink)\b", RegexOptions.IgnoreCase);
        Assert.DoesNotMatch(pattern, ReadmeContent);
    }

    // ── Trash disable dialog functions ──────────────────────────────────

    [Fact]
    public void Html_ContainsShowTrashDisableDialogFunction()
    {
        Assert.Contains("function showTrashDisableDialog", HtmlContent);
    }

    [Fact]
    public void Html_ContainsShowTrashDeleteConfirmationFunction()
    {
        Assert.Contains("function showTrashDeleteConfirmation", HtmlContent);
    }

    [Fact]
    public void Html_ContainsRemoveTrashDialogFunction()
    {
        Assert.Contains("function removeTrashDialog", HtmlContent);
    }

    [Fact]
    public void Html_ContainsWasTrashEnabledVariable()
    {
        Assert.Contains("_wasTrashEnabled", HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_CallsGetTrashFoldersEndpoint()
    {
        Assert.Contains("JellyfinHelper/Trash/Folders", HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_CallsDeleteTrashFoldersEndpoint()
    {
        // Verify the DELETE method call to trash folders endpoint exists
        Assert.Contains("type: 'DELETE', url: apiClient.getUrl('JellyfinHelper/Trash/Folders')", HtmlContent);
    }

    [Fact]
    public void Html_SaveSettings_ChecksTrashDisableCondition()
    {
        // The saveSettings function should check if trash was enabled and is now being disabled
        Assert.Contains("_wasTrashEnabled && !payload.UseTrash", HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_HasKeepAndDeleteButtons()
    {
        // Verify the dialog references the keep and delete translation keys
        Assert.Contains("trashKeep", HtmlContent);
        Assert.Contains("trashDelete", HtmlContent);
    }

    [Fact]
    public void Html_TrashDeleteConfirmation_HasConfirmAndCancelButtons()
    {
        // Verify the confirmation dialog references the confirm OK and cancel translation keys
        Assert.Contains("trashDeleteConfirmOk", HtmlContent);
        Assert.Contains("cancel", HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_SetsWasTrashEnabledOnLoad()
    {
        // Verify _wasTrashEnabled is set from config during load
        Assert.Matches(new Regex(@"_wasTrashEnabled\s*=\s*!!cfg\.UseTrash"), HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_UpdatesWasTrashEnabledOnSave()
    {
        // After a successful save, _wasTrashEnabled should be updated to the new value
        Assert.Matches(new Regex(@"_wasTrashEnabled\s*=\s*payload\.UseTrash"), HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_CancelReChecksTrashCheckbox()
    {
        // When the user cancels, the trash checkbox should be re-checked
        Assert.Contains("cfgTrash", HtmlContent);
    }

    // ── Trash toggle triggers UI rebuild ────────────────────────────────

    [Fact]
    public void Html_DoSaveSettings_DetectsTrashChanged()
    {
        // The doSaveSettings function should detect when the trash toggle changed
        Assert.Contains("var trashChanged = (!!payload.UseTrash) !== _wasTrashEnabled", HtmlContent);
    }

    [Fact]
    public void Html_DoSaveSettings_RebuildUIOnTrashChange()
    {
        // When trash changed (but language didn't), rebuildUI should be called
        Assert.Contains("langChanged || trashChanged", HtmlContent);
    }

    [Fact]
    public void Html_DoSaveSettings_TrashChangedBeforeUpdate()
    {
        // trashChanged must be computed BEFORE _wasTrashEnabled is updated
        var trashChangedPos = HtmlContent.IndexOf("var trashChanged = (!!payload.UseTrash) !== _wasTrashEnabled", StringComparison.Ordinal);
        var wasTrashUpdatePos = HtmlContent.IndexOf("_wasTrashEnabled = payload.UseTrash", trashChangedPos + 1, StringComparison.Ordinal);
        Assert.True(trashChangedPos >= 0, "trashChanged detection not found");
        Assert.True(wasTrashUpdatePos > trashChangedPos, "_wasTrashEnabled must be updated AFTER trashChanged is computed");
    }

    [Fact]
    public void Html_DoSaveSettings_LangChangedVariable()
    {
        // The doSaveSettings function should have a langChanged variable
        Assert.Contains("var langChanged = newLang !== _currentLang", HtmlContent);
    }

    // ── Trash Health Section in Health tab ───────────────────────────────

    [Fact]
    public void Html_ContainsLoadTrashHealthSectionFunction()
    {
        Assert.Contains("function loadTrashHealthSection", HtmlContent);
    }

    [Fact]
    public void Html_LoadTrashHealthSection_ChecksConfigFirst()
    {
        // Should check UseTrash config before loading trash contents
        Assert.Contains("cfg.UseTrash", HtmlContent);
    }

    [Fact]
    public void Html_LoadTrashHealthSection_CallsTrashContentsEndpoint()
    {
        Assert.Contains("JellyfinHelper/Trash/Contents", HtmlContent);
    }

    [Fact]
    public void Html_LoadTrashHealthSection_UsesTrashI18nKeys()
    {
        // Verify key trash health i18n keys are referenced
        Assert.Contains("trashContents", HtmlContent);
        Assert.Contains("trashItems", HtmlContent);
        Assert.Contains("trashTotalSize", HtmlContent);
        Assert.Contains("trashRetentionDays", HtmlContent);
        Assert.Contains("trashEmpty", HtmlContent);
    }

    [Fact]
    public void Html_FillScanData_CallsLoadTrashHealthSection()
    {
        // After filling health data, loadTrashHealthSection should be invoked
        Assert.Contains("loadTrashHealthSection()", HtmlContent);
    }

    [Fact]
    public void Html_TrashDisableDialog_HasAllI18nKeys()
    {
        // Verify all trash dialog i18n keys exist
        var expectedKeys = new[]
        {
            "trashDisableTitle",
            "trashDisablePrompt",
            "trashDisableQuestion",
            "trashKeep",
            "trashDelete",
            "trashDeleteConfirmTitle",
            "trashDeleteConfirmMsg",
            "trashDeleteConfirmWarn",
            "trashDeleteConfirmOk",
            "trashDeleting",
            "trashDeletedCount",
            "trashFailedCount",
            "trashDeleteError"
        };

        foreach (var key in expectedKeys)
        {
            Assert.Contains(key, HtmlContent);
        }
    }

    // ── Trash settings fields ───────────────────────────────────────────

    [Theory]
    [InlineData("cfgTrash", "UseTrash")]
    [InlineData("cfgTrashPath", "TrashFolderPath")]
    [InlineData("cfgTrashDays", "TrashRetentionDays")]
    public void Html_ContainsTrashSettingsElement(string elementId, string configProperty)
    {
        Assert.Contains(elementId, HtmlContent);
        Assert.Contains($"cfg.{configProperty}", HtmlContent);
    }

    [Theory]
    [InlineData("UseTrash")]
    [InlineData("TrashFolderPath")]
    [InlineData("TrashRetentionDays")]
    public void Html_SavesTrashSettingsInPayload(string configProperty)
    {
        Assert.Contains($"{configProperty}:", HtmlContent);
    }
}
