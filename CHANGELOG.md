# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.0.1] — 2026-04-09

### Added

#### 📊 Dashboard Enhancements
- **Audio Codec Analysis** — Audio codecs (AAC, FLAC, MP3, Opus, DTS, AC3, TrueHD, Vorbis, ALAC, PCM, WMA, APE, WavPack, DSD) are parsed from filenames and extensions, displayed as a donut chart.
- **Export as JSON/CSV** — "Export JSON" and "Export CSV" buttons download the complete statistics as a file (`/JellyfinCleaner/Statistics/Export/Json` and `/JellyfinCleaner/Statistics/Export/Csv`).
- **Historical Trend** — Statistics are saved as a snapshot on every scan in a JSON file (max. 365 entries). A trend graph shows library growth over time (`/JellyfinCleaner/Statistics/History`).

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

### Changed
- **MediaExtensions** — Added `AudioExtensionToCodec` dictionary for consistent codec mapping of all audio extensions.
- **LibraryStatistics** — New properties: `AudioCodecs`, `AudioCodecSizes`, `ContainerFormats`, `ContainerSizes`, `VideoCodecs`, `Resolutions`, health check properties (`VideosWithoutSubtitles`, `VideosWithoutImages`, `VideosWithoutNfo`, `OrphanedMetadataDirectories`).
- **MediaStatisticsResult** — Aggregation properties: `TotalVideoFileCount`, `TotalAudioFileCount`, `TotalMusicAudioSize`, `TotalSubtitleSize`, `TotalImageSize`, `TotalNfoSize`, `TotalTrickplaySize`, `ScanTimestamp`.
- **MediaStatisticsService** — Audio codec parsing, container format tracking, video codec and resolution detection, per-directory health check analysis. Refactored to use shared `FileSystemHelper` utilities.
- **CleanTrickplayTask / CleanEmptyMediaFoldersTask** — Refactored to use shared `LibraryPathResolver` for deduplicated library path resolution.
- **Dashboard UI** — Audio codec donut chart, export buttons, trend graph, extended statistics display.

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