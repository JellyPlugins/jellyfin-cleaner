# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.2] — 2026-04-09

### Fixed
- **Critical: Music and Boxset libraries no longer processed by cleanup tasks** — Music libraries and Boxset/Collection libraries are now automatically excluded from all cleanup tasks (Empty Media Folder Cleaner, Trickplay Cleaner, Orphaned Subtitle Cleaner). Previously, music folders were incorrectly flagged as "empty" (since they contain audio, not video files), and Jellyfin's internal collection/boxset folders were at risk of deletion.

### Changed
- **CleanupConfigHelper** — `GetFilteredLibraryLocations()` now filters out `music` and `boxsets` collection types before applying whitelist/blacklist rules.
- **Test coverage** increased to 181 tests (3 new tests for library type filtering).

## [2.0.1] — 2026-04-09

### Added

#### 📊 Dashboard Enhancements
- **Audio Codec Analysis** — Audio codecs (AAC, FLAC, MP3, Opus, DTS, AC3, TrueHD, Vorbis, ALAC, PCM, WMA, APE, WavPack, DSD) are parsed from filenames and extensions, displayed as a donut chart.
- **Export as JSON/CSV** — "Export JSON" and "Export CSV" buttons download the complete statistics as a file (`/JellyfinHelper/Statistics/Export/Json` and `/JellyfinHelper/Statistics/Export/Csv`).
- **Historical Trend** — Statistics are saved as a snapshot on every scan in a JSON file (max. 365 entries). A trend graph shows library growth over time (`/JellyfinHelper/Statistics/History`).
- **Cleanup Statistics** — Dashboard shows lifetime bytes freed, total items deleted, and last cleanup timestamp.

#### 🧹 Orphaned Subtitle Cleaner
- **Orphaned Subtitle Cleaner** — New scheduled task that detects and removes orphaned subtitle files (`.srt`, `.sub`, `.ssa`, `.ass`, `.vtt`, etc.) that no longer have a corresponding video file. Includes a dry-run variant.

#### 🗑️ Trash / Recycle Bin
- **Trash Service** — Instead of permanent deletion, files and folders can be moved to a timestamped trash folder. Expired items are automatically purged after a configurable retention period (default: 30 days).

#### 🔗 Arr Stack Integration
- **Radarr/Sonarr Comparison** — Compare Jellyfin library contents with Radarr and Sonarr to find items in both, items only in Arr (with/without file), and items only in Jellyfin. Configurable via API URL and API key in plugin settings.

#### 🌐 Internationalization
- **Multi-language Dashboard** — UI translations for 7 languages: English, German, French, Spanish, Portuguese, Chinese, Turkish. Configurable in plugin settings.

#### ⚙️ Configuration & Flexibility
- **Library Whitelist / Blacklist** — Include or exclude specific libraries from cleanup tasks via comma-separated configuration.
- **Orphan Minimum Age** — Configurable minimum age (days) before orphaned items are eligible for deletion, protecting against race conditions with active downloads.
- **Dry-Run by Default** — Global toggle to make all cleanup tasks log-only by default.
- **Cleanup Tracking Service** — Persists lifetime cleanup statistics (bytes freed, items deleted) in plugin configuration.
- **CleanupConfigHelper** — Centralized helper for applying plugin configuration rules to all cleanup tasks.

#### 📦 Release / Distribution
- **Automated GitHub Releases** — `release.yml` automatically creates a ZIP archive with checksum and updates `manifest.json` with the correct `sourceUrl` and `checksum`.
- **Docker-based E2E Tests** — CI workflow template for real Jellyfin server integration to test the plugin endpoint.
- **CHANGELOG.md** — Separate file with detailed version history in Keep a Changelog format.

#### 🔐 Security / Robustness
- **Rate Limiting** — The statistics endpoint is protected against excessive requests (min. 30 seconds between scans, HTTP 429 on violation).
- **Input Validation** — `PathValidator` class with path traversal protection (`..` detection, null-byte check, base directory validation) and filename sanitization.
- **Graceful Handling of Large Libraries** — Fault-tolerant processing with `try/catch` per directory; `IOException` and `UnauthorizedAccessException` are logged and skipped.
- **Caching** — Statistics are cached for 5 minutes with `IMemoryCache`. Repeated requests do not trigger a new scan. Use `forceRefresh=true` parameter to bypass cache.

#### 🔧 Code Quality
- **Shared Utilities** — Extracted `LibraryPathResolver` for deduplicated library path resolution and `FileSystemHelper` for reusable filesystem operations (`CalculateDirectorySize`, dictionary increment helpers).
- **Comprehensive Tests** — Added test suites for TrashService, CleanupConfigHelper, I18nService, and ArrComparisonResult (178 total tests).

### Changed
- **MediaExtensions** — Added `AudioExtensionToCodec` dictionary for consistent codec mapping of all audio extensions.
- **LibraryStatistics** — New properties: `AudioCodecs`, `AudioCodecSizes`, `ContainerFormats`, `ContainerSizes`, `VideoCodecs`, `Resolutions`, health check properties (`VideosWithoutSubtitles`, `VideosWithoutImages`, `VideosWithoutNfo`, `OrphanedMetadataDirectories`).
- **MediaStatisticsResult** — Aggregation properties: `TotalVideoFileCount`, `TotalAudioFileCount`, `TotalMusicAudioSize`, `TotalSubtitleSize`, `TotalImageSize`, `TotalNfoSize`, `TotalTrickplaySize`, `ScanTimestamp`.
- **MediaStatisticsService** — Audio codec parsing, container format tracking, video codec and resolution detection, per-directory health check analysis. Refactored to use shared `FileSystemHelper` utilities.
- **CleanTrickplayTask / CleanEmptyMediaFoldersTask** — Refactored to use shared `LibraryPathResolver` for deduplicated library path resolution. Now supports library filtering, orphan age check, trash mode, and cleanup tracking.
- **PluginConfiguration** — Extended with properties for library filtering, trash settings, subtitle cleaner, Arr integration, language, and cleanup tracking.
- **Dashboard UI** — Audio codec donut chart, export buttons, trend graph, extended statistics display, settings tab, Arr integration tab, multi-language support.

## [2.0.0] — 2026-04-09

### Changed
- **Breaking:** Renamed project to Jellyfin Helper.

### Added
- Media library statistics page with per-library breakdown.
- Video codec and resolution parsing from filenames.
- Dashboard with donut charts and library overview.
- Trickplay folder cleanup and empty media folder cleanup.
- Dry-run modes for both cleanup tasks.

## [1.0.1] — 2026-04-09

### Changed
- Improvements and bug fixes.

## [1.0.0] — 2026-04-09

### Added
- Initial version with trickplay folder cleanup.
- Empty media folder cleanup.
- Dry-run modes for both cleanup tasks.