using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Jellyfin.Plugin.JellyfinHelper.Services;

/// <summary>
/// Provides internationalization (i18n) support for the plugin dashboard.
/// Supports: en, de, fr, es, pt, zh, tr.
/// </summary>
public static class I18nService
{
    /// <summary>
    /// Gets the list of supported language codes.
    /// </summary>
    public static ReadOnlyCollection<string> SupportedLanguages { get; } = new List<string>
    {
        "en", "de", "fr", "es", "pt", "zh", "tr"
    }.AsReadOnly();

    /// <summary>
    /// Gets all translation strings for the specified language.
    /// Falls back to English for unknown languages.
    /// </summary>
    /// <param name="languageCode">The ISO 639-1 language code.</param>
    /// <returns>A dictionary of translation keys to translated strings.</returns>
    public static Dictionary<string, string> GetTranslations(string? languageCode)
    {
        var lang = (languageCode ?? "en").ToLowerInvariant();
        return lang switch
        {
            "de" => German(),
            "fr" => French(),
            "es" => Spanish(),
            "pt" => Portuguese(),
            "zh" => Chinese(),
            "tr" => Turkish(),
            _ => English(),
        };
    }

    private static Dictionary<string, string> English() => new(StringComparer.Ordinal)
    {
        // General
        ["title"] = "Jellyfin Helper — Media Statistics",
        ["scanLibraries"] = "Scan Libraries",
        ["scanning"] = "Scanning…",
        ["scanDescription"] = "Scanning libraries… This may take a while for large collections.",
        ["scanPlaceholder"] = "Click <strong>Scan Libraries</strong> to analyze your media folders.",
        ["error"] = "Failed to load statistics. Make sure you are an administrator.",

        // Tabs
        ["tabOverview"] = "Overview",
        ["tabCodecs"] = "Codecs",
        ["tabHealth"] = "Health",
        ["tabTrends"] = "Trends",
        ["tabSettings"] = "Settings",
        ["tabArr"] = "Arr Integration",

        // Overview cards
        ["movieVideoData"] = "Video Data — Movies",
        ["tvVideoData"] = "Video Data — TV Shows",
        ["musicAudio"] = "Music / Audio",
        ["trickplayData"] = "Trickplay Data",
        ["subtitles"] = "Subtitles",
        ["totalFiles"] = "Total Files",
        ["files"] = "files",
        ["file"] = "file",
        ["folders"] = "folders",
        ["folder"] = "folder",
        ["libraries"] = "libraries",
        ["library"] = "library",
        ["episodes"] = "episodes",
        ["episode"] = "episode",
        ["videos"] = "videos",
        ["audio"] = "audio",
        ["mediaFiles"] = "media files",
        ["mediaFile"] = "media file",

        // Storage
        ["storageDistribution"] = "Storage Distribution",
        ["perLibrary"] = "Per-Library Breakdown",
        ["video"] = "Video",
        ["audioLabel"] = "Audio",
        ["images"] = "Images",
        ["trickplay"] = "Trickplay",
        ["metadata"] = "Metadata",
        ["other"] = "Other",
        ["total"] = "Total",
        ["type"] = "Type",

        // Cleanup stats
        ["cleanupStatistics"] = "Cleanup Statistics",
        ["totalBytesFreed"] = "Total Space Freed",
        ["totalItemsDeleted"] = "Total Items Deleted",
        ["lastCleanup"] = "Last Cleanup",
        ["never"] = "Never",
        ["items"] = "items",
        ["trashContents"] = "Trash Contents",
        ["trashItems"] = "items in trash",

        // Codecs
        ["videoCodecs"] = "Video Codecs",
        ["audioCodecs"] = "Audio Codecs",
        ["containerFormats"] = "Container Formats",
        ["resolutions"] = "Resolutions",
        ["noData"] = "No data",

        // Health
        ["healthChecks"] = "Library Health Checks",
        ["noSubtitles"] = "Videos without subtitles",
        ["noImages"] = "Videos without images",
        ["noNfo"] = "Videos without NFO",
        ["orphanedDirs"] = "Orphaned metadata dirs",

        // Trends
        ["growthTrend"] = "Library Growth Trend",
        ["trendEmpty"] = "Not enough historical data yet. Trend data is collected with each scan.",
        ["trendLoading"] = "Loading trend data…",
        ["trendError"] = "Could not load trend data.",

        // Settings
        ["settingsTitle"] = "Plugin Settings",
        ["includedLibraries"] = "Included Libraries (whitelist, comma-separated)",
        ["includedLibrariesHelp"] = "Leave empty to include all libraries.",
        ["excludedLibraries"] = "Excluded Libraries (blacklist, comma-separated)",
        ["orphanMinAge"] = "Orphan Minimum Age (days)",
        ["orphanMinAgeHelp"] = "Orphaned items younger than this will not be deleted. Protects against race conditions with active downloads.",
        ["dryRunDefault"] = "Dry-Run Mode by Default",
        ["dryRunHelp"] = "When enabled, cleanup tasks will only log what would be deleted without actually deleting.",
        ["enableSubtitleCleaner"] = "Enable Orphaned Subtitle Cleaner",
        ["useTrash"] = "Use Trash (Recycle Bin) instead of permanent delete",
        ["trashFolder"] = "Trash Folder Path",
        ["trashFolderHelp"] = "Relative to library root, or absolute path.",
        ["trashRetention"] = "Trash Retention (days)",
        ["language"] = "Dashboard Language",
        ["radarrUrl"] = "Radarr URL",
        ["radarrApiKey"] = "Radarr API Key",
        ["sonarrUrl"] = "Sonarr URL",
        ["sonarrApiKey"] = "Sonarr API Key",
        ["saveSettings"] = "Save Settings",
        ["settingsSaved"] = "Settings saved successfully!",
        ["settingsError"] = "Failed to save settings.",

        // Arr Integration
        ["arrTitle"] = "Arr Stack Integration",
        ["compareRadarr"] = "Compare with Radarr",
        ["compareSonarr"] = "Compare with Sonarr",
        ["inBoth"] = "In Both",
        ["inArrOnly"] = "In Arr Only (with file)",
        ["inArrOnlyMissing"] = "In Arr Only (no file)",
        ["inJellyfinOnly"] = "In Jellyfin Only",
        ["arrNotConfigured"] = "Not configured. Please set URL and API key in Settings.",
        ["comparing"] = "Comparing…",

        // Export
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> German() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — Medienstatistiken",
        ["scanLibraries"] = "Bibliotheken scannen",
        ["scanning"] = "Scanne…",
        ["scanDescription"] = "Bibliotheken werden gescannt… Dies kann bei großen Sammlungen eine Weile dauern.",
        ["scanPlaceholder"] = "Klicken Sie auf <strong>Bibliotheken scannen</strong>, um Ihre Medienordner zu analysieren.",
        ["error"] = "Statistiken konnten nicht geladen werden. Stellen Sie sicher, dass Sie Administrator sind.",
        ["tabOverview"] = "Übersicht",
        ["tabCodecs"] = "Codecs",
        ["tabHealth"] = "Zustand",
        ["tabTrends"] = "Trends",
        ["tabSettings"] = "Einstellungen",
        ["tabArr"] = "Arr-Integration",
        ["movieVideoData"] = "Videodaten — Filme",
        ["tvVideoData"] = "Videodaten — Serien",
        ["musicAudio"] = "Musik / Audio",
        ["trickplayData"] = "Trickplay-Daten",
        ["subtitles"] = "Untertitel",
        ["totalFiles"] = "Dateien gesamt",
        ["files"] = "Dateien",
        ["file"] = "Datei",
        ["folders"] = "Ordner",
        ["folder"] = "Ordner",
        ["libraries"] = "Bibliotheken",
        ["library"] = "Bibliothek",
        ["episodes"] = "Episoden",
        ["episode"] = "Episode",
        ["videos"] = "Videos",
        ["audio"] = "Audio",
        ["mediaFiles"] = "Mediendateien",
        ["mediaFile"] = "Mediendatei",
        ["storageDistribution"] = "Speicherverteilung",
        ["perLibrary"] = "Pro Bibliothek",
        ["video"] = "Video",
        ["audioLabel"] = "Audio",
        ["images"] = "Bilder",
        ["trickplay"] = "Trickplay",
        ["metadata"] = "Metadaten",
        ["other"] = "Sonstiges",
        ["total"] = "Gesamt",
        ["type"] = "Typ",
        ["cleanupStatistics"] = "Aufräumstatistiken",
        ["totalBytesFreed"] = "Insgesamt freigegebener Speicher",
        ["totalItemsDeleted"] = "Insgesamt gelöschte Elemente",
        ["lastCleanup"] = "Letztes Aufräumen",
        ["never"] = "Nie",
        ["items"] = "Elemente",
        ["trashContents"] = "Papierkorb-Inhalt",
        ["trashItems"] = "Elemente im Papierkorb",
        ["videoCodecs"] = "Video-Codecs",
        ["audioCodecs"] = "Audio-Codecs",
        ["containerFormats"] = "Container-Formate",
        ["resolutions"] = "Auflösungen",
        ["noData"] = "Keine Daten",
        ["healthChecks"] = "Bibliothek-Gesundheitschecks",
        ["noSubtitles"] = "Videos ohne Untertitel",
        ["noImages"] = "Videos ohne Bilder",
        ["noNfo"] = "Videos ohne NFO",
        ["orphanedDirs"] = "Verwaiste Metadaten-Ordner",
        ["growthTrend"] = "Bibliothekswachstum",
        ["trendEmpty"] = "Noch nicht genügend historische Daten. Trenddaten werden bei jedem Scan gesammelt.",
        ["trendLoading"] = "Lade Trenddaten…",
        ["trendError"] = "Trenddaten konnten nicht geladen werden.",
        ["settingsTitle"] = "Plugin-Einstellungen",
        ["includedLibraries"] = "Eingeschlossene Bibliotheken (Whitelist, kommagetrennt)",
        ["includedLibrariesHelp"] = "Leer lassen, um alle Bibliotheken einzuschließen.",
        ["excludedLibraries"] = "Ausgeschlossene Bibliotheken (Blacklist, kommagetrennt)",
        ["orphanMinAge"] = "Mindestalter für Waisen (Tage)",
        ["orphanMinAgeHelp"] = "Verwaiste Elemente jünger als diese Anzahl Tage werden nicht gelöscht. Schützt vor Race Conditions bei aktiven Downloads.",
        ["dryRunDefault"] = "Standardmäßig Probelauf",
        ["dryRunHelp"] = "Wenn aktiviert, protokollieren Aufräumaufgaben nur, was gelöscht würde, ohne tatsächlich zu löschen.",
        ["enableSubtitleCleaner"] = "Verwaisten-Untertitel-Bereiniger aktivieren",
        ["useTrash"] = "Papierkorb statt dauerhaftem Löschen verwenden",
        ["trashFolder"] = "Papierkorb-Ordnerpfad",
        ["trashFolderHelp"] = "Relativ zum Bibliotheksstamm oder absoluter Pfad.",
        ["trashRetention"] = "Papierkorb-Aufbewahrung (Tage)",
        ["language"] = "Dashboard-Sprache",
        ["radarrUrl"] = "Radarr-URL",
        ["radarrApiKey"] = "Radarr-API-Schlüssel",
        ["sonarrUrl"] = "Sonarr-URL",
        ["sonarrApiKey"] = "Sonarr-API-Schlüssel",
        ["saveSettings"] = "Einstellungen speichern",
        ["settingsSaved"] = "Einstellungen erfolgreich gespeichert!",
        ["settingsError"] = "Einstellungen konnten nicht gespeichert werden.",
        ["arrTitle"] = "Arr-Stack-Integration",
        ["compareRadarr"] = "Mit Radarr vergleichen",
        ["compareSonarr"] = "Mit Sonarr vergleichen",
        ["inBoth"] = "In beiden",
        ["inArrOnly"] = "Nur in Arr (mit Datei)",
        ["inArrOnlyMissing"] = "Nur in Arr (ohne Datei)",
        ["inJellyfinOnly"] = "Nur in Jellyfin",
        ["arrNotConfigured"] = "Nicht konfiguriert. Bitte URL und API-Schlüssel in den Einstellungen festlegen.",
        ["comparing"] = "Vergleiche…",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> French() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — Statistiques des médias",
        ["scanLibraries"] = "Scanner les bibliothèques",
        ["scanning"] = "Analyse…",
        ["scanDescription"] = "Analyse des bibliothèques… Cela peut prendre du temps pour les grandes collections.",
        ["scanPlaceholder"] = "Cliquez sur <strong>Scanner les bibliothèques</strong> pour analyser vos dossiers médias.",
        ["error"] = "Impossible de charger les statistiques. Assurez-vous d'être administrateur.",
        ["tabOverview"] = "Aperçu",
        ["tabCodecs"] = "Codecs",
        ["tabHealth"] = "Santé",
        ["tabTrends"] = "Tendances",
        ["tabSettings"] = "Paramètres",
        ["tabArr"] = "Intégration Arr",
        ["movieVideoData"] = "Données vidéo — Films",
        ["tvVideoData"] = "Données vidéo — Séries TV",
        ["musicAudio"] = "Musique / Audio",
        ["trickplayData"] = "Données Trickplay",
        ["subtitles"] = "Sous-titres",
        ["totalFiles"] = "Total des fichiers",
        ["storageDistribution"] = "Distribution du stockage",
        ["perLibrary"] = "Par bibliothèque",
        ["cleanupStatistics"] = "Statistiques de nettoyage",
        ["totalBytesFreed"] = "Espace total libéré",
        ["totalItemsDeleted"] = "Total des éléments supprimés",
        ["lastCleanup"] = "Dernier nettoyage",
        ["never"] = "Jamais",
        ["healthChecks"] = "Vérifications de santé",
        ["noSubtitles"] = "Vidéos sans sous-titres",
        ["noImages"] = "Vidéos sans images",
        ["noNfo"] = "Vidéos sans NFO",
        ["orphanedDirs"] = "Répertoires de métadonnées orphelins",
        ["settingsTitle"] = "Paramètres du plugin",
        ["saveSettings"] = "Enregistrer",
        ["settingsSaved"] = "Paramètres enregistrés!",
        ["settingsError"] = "Erreur lors de l'enregistrement.",
        ["arrTitle"] = "Intégration Arr Stack",
        ["compareRadarr"] = "Comparer avec Radarr",
        ["compareSonarr"] = "Comparer avec Sonarr",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> Spanish() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — Estadísticas de medios",
        ["scanLibraries"] = "Escanear bibliotecas",
        ["scanning"] = "Escaneando…",
        ["scanDescription"] = "Escaneando bibliotecas… Esto puede tardar para colecciones grandes.",
        ["scanPlaceholder"] = "Haz clic en <strong>Escanear bibliotecas</strong> para analizar tus carpetas de medios.",
        ["error"] = "Error al cargar estadísticas. Asegúrate de ser administrador.",
        ["tabOverview"] = "Resumen",
        ["tabCodecs"] = "Códecs",
        ["tabHealth"] = "Salud",
        ["tabTrends"] = "Tendencias",
        ["tabSettings"] = "Configuración",
        ["tabArr"] = "Integración Arr",
        ["cleanupStatistics"] = "Estadísticas de limpieza",
        ["totalBytesFreed"] = "Espacio total liberado",
        ["totalItemsDeleted"] = "Total de elementos eliminados",
        ["lastCleanup"] = "Última limpieza",
        ["never"] = "Nunca",
        ["settingsTitle"] = "Configuración del plugin",
        ["saveSettings"] = "Guardar",
        ["settingsSaved"] = "¡Configuración guardada!",
        ["settingsError"] = "Error al guardar la configuración.",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> Portuguese() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — Estatísticas de mídia",
        ["scanLibraries"] = "Escanear bibliotecas",
        ["scanning"] = "Escaneando…",
        ["scanDescription"] = "Escaneando bibliotecas… Pode demorar para coleções grandes.",
        ["scanPlaceholder"] = "Clique em <strong>Escanear bibliotecas</strong> para analisar suas pastas de mídia.",
        ["error"] = "Falha ao carregar estatísticas. Certifique-se de ser administrador.",
        ["tabOverview"] = "Visão geral",
        ["tabCodecs"] = "Codecs",
        ["tabHealth"] = "Saúde",
        ["tabTrends"] = "Tendências",
        ["tabSettings"] = "Configurações",
        ["tabArr"] = "Integração Arr",
        ["cleanupStatistics"] = "Estatísticas de limpeza",
        ["totalBytesFreed"] = "Espaço total liberado",
        ["totalItemsDeleted"] = "Total de itens excluídos",
        ["lastCleanup"] = "Última limpeza",
        ["never"] = "Nunca",
        ["settingsTitle"] = "Configurações do plugin",
        ["saveSettings"] = "Salvar",
        ["settingsSaved"] = "Configurações salvas!",
        ["settingsError"] = "Erro ao salvar configurações.",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> Chinese() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — 媒体统计",
        ["scanLibraries"] = "扫描媒体库",
        ["scanning"] = "扫描中…",
        ["scanDescription"] = "正在扫描媒体库…大型收藏可能需要一些时间。",
        ["scanPlaceholder"] = "点击 <strong>扫描媒体库</strong> 来分析您的媒体文件夹。",
        ["error"] = "加载统计信息失败。请确保您是管理员。",
        ["tabOverview"] = "概览",
        ["tabCodecs"] = "编解码器",
        ["tabHealth"] = "健康",
        ["tabTrends"] = "趋势",
        ["tabSettings"] = "设置",
        ["tabArr"] = "Arr 集成",
        ["cleanupStatistics"] = "清理统计",
        ["totalBytesFreed"] = "已释放总空间",
        ["totalItemsDeleted"] = "已删除总项目",
        ["lastCleanup"] = "上次清理",
        ["never"] = "从未",
        ["settingsTitle"] = "插件设置",
        ["saveSettings"] = "保存设置",
        ["settingsSaved"] = "设置已保存！",
        ["settingsError"] = "保存设置失败。",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };

    private static Dictionary<string, string> Turkish() => new(StringComparer.Ordinal)
    {
        ["title"] = "Jellyfin Helper — Medya İstatistikleri",
        ["scanLibraries"] = "Kütüphaneleri Tara",
        ["scanning"] = "Taranıyor…",
        ["scanDescription"] = "Kütüphaneler taranıyor… Büyük koleksiyonlar için biraz zaman alabilir.",
        ["scanPlaceholder"] = "Medya klasörlerinizi analiz etmek için <strong>Kütüphaneleri Tara</strong>'ya tıklayın.",
        ["error"] = "İstatistikler yüklenemedi. Yönetici olduğunuzdan emin olun.",
        ["tabOverview"] = "Genel Bakış",
        ["tabCodecs"] = "Kodekler",
        ["tabHealth"] = "Sağlık",
        ["tabTrends"] = "Trendler",
        ["tabSettings"] = "Ayarlar",
        ["tabArr"] = "Arr Entegrasyonu",
        ["cleanupStatistics"] = "Temizleme İstatistikleri",
        ["totalBytesFreed"] = "Toplam Boşaltılan Alan",
        ["totalItemsDeleted"] = "Toplam Silinen Öğe",
        ["lastCleanup"] = "Son Temizleme",
        ["never"] = "Hiç",
        ["settingsTitle"] = "Eklenti Ayarları",
        ["saveSettings"] = "Ayarları Kaydet",
        ["settingsSaved"] = "Ayarlar başarıyla kaydedildi!",
        ["settingsError"] = "Ayarlar kaydedilemedi.",
        ["exportJson"] = "JSON",
        ["exportCsv"] = "CSV",
    };
}