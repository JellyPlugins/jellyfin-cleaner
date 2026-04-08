# Jellyfin Helper

A [Jellyfin](https://jellyfin.org/) plugin that provides automated cleanup tasks and media library statistics for your media library.

## Screenshots

> **📸 Placeholder** — Screenshots of the settings page will be added after the next release.

<!-- TODO: Add screenshots or GIFs of the settings page here
![Dashboard Overview](docs/screenshots/dashboard-overview.png)
![Audio Codec Chart](docs/screenshots/audio-codec-chart.png)
![Trend Graph](docs/screenshots/trend-graph.png)
![Export Buttons](docs/screenshots/export-buttons.png)
-->

## Features

### 🧹 Trickplay Folder Cleaner
Automatically deletes orphaned `.trickplay` folders that no longer have a corresponding media file. This typically happens when media files are renamed, moved, or deleted while the trickplay data remains behind.

### 📁 Empty Media Folder Cleaner
Automatically deletes top-level media folders whose entire directory tree contains files but absolutely **no video files**. This targets the common scenario where a movie or episode is deleted but the surrounding folder with metadata (`.nfo`), artwork (`.jpg`), subtitles (`.srt`), etc. remains as an orphaned folder.

**Important behaviors:**
- **Completely empty folders are skipped** — they are often pre-created by tools like Radarr/Sonarr for upcoming/"wanted" media
- **TV show folders are checked as a whole** — if at least one video exists anywhere in the tree (even in a deeply nested subdirectory), the entire show folder is kept untouched
- **`.trickplay` folders are skipped** — they are handled by the Trickplay Folder Cleaner task

### 📊 Media Library Statistics
A settings page that provides a comprehensive overview of your media library disk usage:
- **Video Data in Movies** / **Video Data in Series** / **Audio Data in Music**
- **Trickplay Data**, **Subtitle Data**, **Image Data**, **NFO/Metadata Data**
- Per-library breakdown with file counts
- **Video Codec Analysis** — HEVC, H.264, AV1, VP9, XviD, DivX, MPEG parsed from filenames
- **Audio Codec Analysis** — AAC, FLAC, MP3, Opus, DTS, AC3, TrueHD, Vorbis, ALAC, PCM, WMA, APE, WavPack, DSD displayed as donut chart
- **Container Formats** — MKV, MP4, AVI, WebM etc. with file count and size
- **Resolution Distribution** — 4K, 1080p, 720p, 480p, 576p
- **Health Check** — Detection of videos without subtitles, without artwork, without NFO, and orphaned metadata directories

### 📈 Export & History
- **Export as JSON** — Download complete statistics as a JSON file
- **Export as CSV** — Download per-library breakdown as a CSV file
- **Historical Trend** — Statistics are saved as a snapshot on every scan (max. 365 entries) and displayed as a trend graph

### 🔐 Security & Performance
- **5-Minute Cache** — Statistics are cached with `IMemoryCache`; repeated clicks do not trigger a new scan
- **Rate Limiting** — Minimum 30 seconds between scans; HTTP 429 returned on excessive requests
- **Input Validation** — Path traversal protection with null-byte detection and base directory validation
- **Graceful Handling** — `IOException` and `UnauthorizedAccessException` are logged and skipped per directory

### 🔍 Dry Run Mode
Both cleanup tasks have a corresponding **Dry Run** variant that logs what *would* be deleted without actually deleting anything. Use these to verify the cleanup behavior before enabling the actual cleanup tasks.

## Scheduled Tasks

| Task | Description | Default Schedule |
|------|-------------|-----------------|
| **Trickplay Folder Cleaner** | Deletes orphaned `.trickplay` folders | Weekly, Sunday 2:00 AM |
| **Trickplay Folder Cleaner (Dry Run)** | Logs orphaned `.trickplay` folders without deleting | No default trigger |
| **Empty Media Folder Cleaner** | Deletes media folders with no video files | Weekly, Sunday 3:00 AM |
| **Empty Media Folder Cleaner (Dry Run)** | Logs empty media folders without deleting | No default trigger |

All tasks appear under the **Jellyfin Helper** category in the Jellyfin scheduled tasks dashboard.

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/JellyfinCleaner/Statistics` | GET | Retrieve statistics (cached for 5 min; use `?forceRefresh=true` to bypass cache) |
| `/JellyfinCleaner/Statistics/Export/Json` | GET | Download statistics as a JSON file |
| `/JellyfinCleaner/Statistics/Export/Csv` | GET | Download statistics as a CSV file |
| `/JellyfinCleaner/Statistics/History` | GET | Retrieve historical snapshots for trend graph |

All endpoints require admin authorization (`RequiresElevation`).

## Supported File Extensions

### Video
`.3g2` `.3gp` `.asf` `.avi` `.divx` `.dvr-ms` `.f4v` `.flv` `.hevc` `.img` `.iso` `.m2ts` `.m2v` `.m4v` `.mk3d` `.mkv` `.mov` `.mp4` `.mpeg` `.mpg` `.mts` `.ogg` `.ogm` `.ogv` `.rec` `.rm` `.rmvb` `.ts` `.vob` `.webm` `.wmv` `.wtv`

### Audio
`.flac` `.mp3` `.ogg` `.opus` `.wav` `.wma` `.m4a` `.aac` `.ape` `.wv` `.dsf` `.dff` `.mka`

### Subtitle
`.srt` `.sub` `.ssa` `.ass` `.vtt` `.idx` `.smi` `.pgs` `.sup`

### Image
`.jpg` `.jpeg` `.png` `.gif` `.bmp` `.webp` `.tbn` `.ico` `.svg`

## Installation

### From Repository (Recommended)

1. In Jellyfin, go to **Dashboard** → **Plugins** → **Repositories**
2. Add this repository URL:
   ```
   https://raw.githubusercontent.com/JellyPlugins/jellyfin-helper/main/manifest.json
   ```
3. Go to **Catalog** and install **Jellyfin Helper**
4. Restart Jellyfin

### Manual Installation

1. Download the latest release from the [Releases](https://github.com/JellyPlugins/jellyfin-helper/releases) page
2. Extract the `.dll` file into your Jellyfin plugins directory (e.g., `/config/plugins/JellyfinHelper/`)
3. Restart Jellyfin

## Usage

1. After installation, go to **Dashboard** → **Scheduled Tasks**
2. Look for tasks under the **Jellyfin Helper** category
3. **Recommended:** Run the **Dry Run** tasks first to review what would be deleted
4. Check the Jellyfin logs to see the results
5. Once satisfied, enable the actual cleanup tasks or run them manually
6. Visit the plugin's **Settings** page to view media library statistics, export data, and review trends

## Building from Source

```bash
dotnet build
dotnet test
```

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for a detailed version history.

## Origin

This project is based on the original [jellyfin-trickplay-folder-cleaner](https://github.com/Noir1992/jellyfin-trickplay-folder-cleaner) by [@Noir1992](https://github.com/Noir1992), which was inspired by [this community script](https://github.com/jellyfin/jellyfin/issues/12818#issuecomment-2712783498).

This fork evolved into an independent project with significant additions including empty media folder cleanup, media library statistics, audio codec analysis, export/history features, caching, rate limiting, comprehensive test coverage, CI/CD pipeline with integration tests, and Dependabot/CodeRabbit integration.

## License

This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

## Acknowledgements
[@Noir1992](https://github.com/Noir1992) — Original plugin author<br />
[@K-Money](https://github.com/K-Money) — Initial Testing