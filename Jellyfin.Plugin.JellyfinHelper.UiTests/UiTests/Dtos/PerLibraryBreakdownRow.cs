namespace Jellyfin.Plugin.JellyfinHelper.UiTests.UiTests.Dtos;

public record PerLibraryBreakdownRow(
    string Library,
    string Type,
    string Video,
    string Audio,
    string Subtitles,
    string Images,
    string Trickplay,
    string Total);