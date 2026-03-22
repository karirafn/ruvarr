# Ruvarr

A media management tool that syncs TV programs and movies from RUV (Icelandic public broadcaster), matches them to TVDB/TMDB metadata, and downloads episodes via FFmpeg. Integrates with Sonarr for media management.

## Prerequisites

- .NET 10 SDK
- Docker Desktop (for containerized running)
- FFmpeg (for local/non-Docker running -- must be on PATH)

## Configuration

### Local development

```bash
dotnet user-secrets set "Tmdb:ApiKey" "your_key" --project src/Ruvarr
dotnet user-secrets set "Tvdb:ApiKey" "your_key" --project src/Ruvarr
```

### Docker

Copy `.env.example` to `.env` and fill in your API keys:

```bash
cp .env.example .env
```

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

Or set `docker-compose` as the startup project in Visual Studio.

The app will be available at `http://localhost:8080`.

## Runtime Settings

Sonarr connection, download directories, and other settings are configured through the Settings UI after the app is running.

## Database

SQLite, stored in `data/ruvarr.db`. Migrations run automatically on startup.

## Tests

```bash
dotnet test Ruvarr.slnx
```
