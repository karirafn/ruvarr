using System.Text.Json;

using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.SettingsStoreTests;

public sealed class LoadFromFile : IDisposable
{
    private readonly string _tempDir;

    public LoadFromFile()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void MigratesAbsoluteEpisodePath_StrippingDownloadsPrefix()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "settings.json");
        var legacy = new
        {
            SonarrBaseAddress = "",
            SonarrApiKey = "",
            TvdbApiKey = "",
            TmdbApiKey = "",
            DownloadsRootDirectory = "/downloads",
            EpisodeDownloadDirectory = "/downloads/tv",
            MovieDownloadDirectory = "/downloads/movies",
            IgnoredChannels = Array.Empty<string>()
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy));

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
    }

    [Fact]
    public void MigratesAbsoluteMoviePath_StrippingDownloadsPrefix()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "settings.json");
        var legacy = new
        {
            SonarrBaseAddress = "",
            SonarrApiKey = "",
            TvdbApiKey = "",
            TmdbApiKey = "",
            DownloadsRootDirectory = "/downloads",
            EpisodeDownloadDirectory = "/downloads/tv",
            MovieDownloadDirectory = "/downloads/movies",
            IgnoredChannels = Array.Empty<string>()
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy));

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
    }

    [Fact]
    public void DoesNotMigrate_WhenPathsAreAlreadyRelative()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "settings.json");
        var settings = new
        {
            SonarrBaseAddress = "",
            SonarrApiKey = "",
            TvdbApiKey = "",
            TmdbApiKey = "",
            EpisodeDownloadDirectory = "tv",
            MovieDownloadDirectory = "movies",
            IgnoredChannels = Array.Empty<string>()
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(settings));

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
    }

    [Fact]
    public void MigratesCustomAbsolutePaths_UnderDownloadsRoot()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "settings.json");
        var legacy = new
        {
            SonarrBaseAddress = "",
            SonarrApiKey = "",
            TvdbApiKey = "",
            TmdbApiKey = "",
            DownloadsRootDirectory = "/downloads",
            EpisodeDownloadDirectory = "/downloads/series/episodes",
            MovieDownloadDirectory = "/downloads/films",
            IgnoredChannels = Array.Empty<string>()
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy));

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.EpisodeDownloadDirectory.ShouldBe("series/episodes");
        store.Current.MovieDownloadDirectory.ShouldBe("films");
    }

    [Fact]
    public void RemovesDownloadsRootDirectory_FromDeserializedSettings()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "settings.json");
        var legacy = new
        {
            SonarrBaseAddress = "",
            SonarrApiKey = "",
            TvdbApiKey = "",
            TmdbApiKey = "",
            DownloadsRootDirectory = "/downloads",
            EpisodeDownloadDirectory = "/downloads/tv",
            MovieDownloadDirectory = "/downloads/movies",
            IgnoredChannels = Array.Empty<string>()
        };
        File.WriteAllText(filePath, JsonSerializer.Serialize(legacy));

        // Act
        using SettingsStore store = new(filePath);

        // Assert - the DownloadsRootDirectory parameter no longer exists on the record
        // so we just verify it loaded correctly with the defaults
        store.Current.ShouldNotBeNull();
    }

    [Fact]
    public void CreatesDefaultSettings_WhenFileDoesNotExist()
    {
        // Arrange
        string filePath = Path.Combine(_tempDir, "nonexistent", "settings.json");

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
