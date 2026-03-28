# Ruvarr

Ruvarr automates downloading RÚV (Icelandic public broadcaster) content into a Sonarr-managed library. It syncs the RÚV program catalogue, matches shows and movies to TVDB/TMDB metadata, identifies which episodes are missing from your Sonarr library, downloads them via FFmpeg, and imports them into Sonarr automatically.

## How it works

### Program sync

Every hour Ruvarr fetches the full RÚV program catalogue and keeps a local copy up to date — adding new programs, updating metadata, and removing programs that are no longer available. Multi-episode programs are queued for episode sync, which runs every 5 seconds and pulls the current episode list from the RÚV API.

### TVDB series matching

Multi-episode programs are matched to a TVDB series so episode numbers can be resolved. Matching runs automatically every 5 seconds and tries several strategies in order:

1. **Icelandic translation** — exact match against a series' registered Icelandic name on TVDB
2. **English / original name** — case-insensitive match with accent normalization and punctuation stripping
3. **Numeral stripping** — retries after removing trailing Roman numerals or numbers from the program name
4. **Foreign name** — same strategies applied to the program's foreign/original-language name
5. **Episode translation disambiguation** — when multiple candidate series remain, fetches Icelandic episode translations from TVDB and picks the series whose episode titles match RÚV's

If no match is found, the program is retried with exponential backoff (1 h → 2 h → 4 h → 1 d → 7 d). Programs can also be matched manually via the UI, which searches TVDB by name and lets you pick the correct series or enter a TVDB ID directly.

### TVDB episode matching

Once a program is matched to a series, its episodes are matched to specific TVDB season/episode entries. Three strategies run in order:

1. **Icelandic translation** — fetches Icelandic episode translations from TVDB and matches by title (most reliable)
2. **Episode number** — extracts the episode number from the RÚV title (Icelandic: "þáttur N") and resolves the correct season from the series structure
3. **Part-two sibling** — detects two-part episodes (", fyrri/síðari hluti") and resolves part two by finding the already-matched part one

Unmatched episodes retry on the same exponential backoff schedule. Manual matching is available per-episode in the UI.

### TMDB movie matching

Programs flagged as `has_multiple_episodes = false` by the RÚV API are treated as movies and matched against TMDB. A match requires an exact title match (or a matching Icelandic translation). Movies are not downloaded yet — Radarr integration is planned.

### Downloads and Sonarr import

When a matched episode is identified as missing from Sonarr (by comparing matched TVDB episode IDs against Sonarr's wanted list), it is added to the download queue automatically. Downloads can also be triggered manually.

The download queue processor runs every 5 seconds:

1. Picks the next pending item and downloads the RÚV stream to disk via FFmpeg (`{DownloadsRoot}/{EpisodeDir}/{SeriesName}/SeriesName S01E01 - RUV.mp4`)
2. Asks Sonarr to scan the downloads folder
3. Sends a manual import request to Sonarr with the matched series ID, episode IDs, quality, and release group (`RUV`)
4. Marks the item complete (or failed if any step errors)

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for containerized running)
- FFmpeg (for local/non-Docker running — must be on PATH)

## Running

### Local

```bash
dotnet run --project src/Ruvarr
```

The app will be available at `http://localhost:5156`.

### Docker Compose

```bash
docker compose up
```

The app will be available at `http://localhost:8080`.

## Configuration

All settings are configured through the Settings UI after the app starts. Settings are persisted to `data/settings.json`.

Required settings:
- **TVDB API key** — for series/episode metadata matching
- **TMDB API key** — for movie metadata matching
- **Sonarr URL and API key** — for missing episode detection and import triggering
- **Downloads root directory** — where downloaded files are written (default: `/downloads`)

## Database

SQLite, stored in `data/ruvarr.db`. Migrations run automatically on startup.

## Tests

```bash
dotnet test Ruvarr.slnx
```
