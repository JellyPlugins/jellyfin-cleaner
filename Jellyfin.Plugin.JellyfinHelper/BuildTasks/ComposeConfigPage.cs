using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Jellyfin.Plugin.JellyfinHelper.BuildTasks;

public class ComposeConfigPage : Task
{
    private static readonly char[] Separator = [';'];

    [Required]
    public string TemplateFile { get; set; } = string.Empty;

    [Required]
    public string CssFiles { get; set; } = string.Empty;

    [Required]
    public string JsFiles { get; set; } = string.Empty;

    [Required]
    public string OutputFile { get; set; } = string.Empty;

    public override bool Execute()
    {
        var template = File.ReadAllText(TemplateFile);

        var cssBuilder = new StringBuilder();
        foreach (var cssFile in CssFiles.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = cssFile.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (!File.Exists(trimmed))
            {
                throw new FileNotFoundException("Configured CSS module was not found.", trimmed);
            }

            cssBuilder.AppendLine(File.ReadAllText(trimmed));
        }

        var jsBuilder = new StringBuilder();
        jsBuilder.AppendLine("(function () {");
        jsBuilder.AppendLine("'use strict';");

        foreach (var jsFile in JsFiles.Split(Separator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = jsFile.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            if (!File.Exists(trimmed))
            {
                throw new FileNotFoundException("Configured JS module was not found.", trimmed);
            }

            jsBuilder.AppendLine(File.ReadAllText(trimmed));
        }

        jsBuilder.AppendLine("})();");

        var result = template
            .Replace("/* CSS_CONTENT */", cssBuilder.ToString())
            .Replace("/* JS_CONTENT */", jsBuilder.ToString());

        var dir = Path.GetDirectoryName(OutputFile);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(OutputFile, result);
        Log.LogMessage(MessageImportance.High, "Composed configPage.html from template + CSS modules + JS modules");

        return true;
    }
}