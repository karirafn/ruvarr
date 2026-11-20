using System.Text.Json;

using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.SettingsStoreTests;

public sealed class ConfiguredDownloadsRoot : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public ConfiguredDownloadsRoot()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    [Fact]
    public void DefaultsToSlashDownloadsWhenNoRootIsConfigured()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");

        // Act
        using SettingsStore store = new(filePath);

        // Assert
        store.Current.DownloadsRoot.ShouldBe("/downloads");
    }

    [Fact]
    public void UsesConfiguredRootWhenFileDoesNotExist()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");

        // Act
        using SettingsStore store = new(filePath, "/data/torrents");

        // Assert
        store.Current.DownloadsRoot.ShouldBe("/data/torrents");
        store.Current.ResolvedEpisodeDownloadDirectory.ShouldBe("/data/torrents/tv");
        store.Current.ResolvedMovieDownloadDirectory.ShouldBe("/data/torrents/movies");
        store.Current.ResolvedIncompleteDirectory.ShouldBe("/data/torrents/incomplete");
    }

    [Fact]
    public void ConfiguredRootWinsOverDownloadsRootInFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(filePath, """{ "DownloadsRoot": "/downloads", "EpisodeDownloadDirectory": "tv" }""");

        // Act
        using SettingsStore store = new(filePath, "/data/torrents");

        // Assert
        store.Current.DownloadsRoot.ShouldBe("/data/torrents");
        store.Current.ResolvedEpisodeDownloadDirectory.ShouldBe("/data/torrents/tv");
    }

    [Fact]
    public void MigratesAbsolutePathsUsingConfiguredRoot()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        File.WriteAllText(filePath, """{ "EpisodeDownloadDirectory": "/data/torrents/tv", "MovieDownloadDirectory": "/data/torrents/movies" }""");

        // Act
        using SettingsStore store = new(filePath, "/data/torrents");

        // Assert
        store.Current.EpisodeDownloadDirectory.ShouldBe("tv");
        store.Current.MovieDownloadDirectory.ShouldBe("movies");
    }

    [Fact]
    public async Task SaveKeepsConfiguredRootWhenSavedSettingsCarryAnotherRoot()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        using SettingsStore store = new(filePath, "/data/torrents");
        RuvarrSettings settings = new("http://localhost:8989", "key", "", "", "tv", "movies");

        // Act
        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Assert
        store.Current.DownloadsRoot.ShouldBe("/data/torrents");
    }

    [Fact]
    public async Task SaveDoesNotWriteDownloadsRootToFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        using SettingsStore store = new(filePath, "/data/torrents");

        // Act
        await store.SaveAsync(store.Current, TestContext.Current.CancellationToken);

        // Assert
        string json = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
        using JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.TryGetProperty("DownloadsRoot", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void RejectsBlankConfiguredRoot(string downloadsRoot)
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");

        // Act
        Exception exception = Should.Throw<ArgumentException>(() => new SettingsStore(filePath, downloadsRoot));

        // Assert
        exception.Message.ShouldContain("downloadsRoot");
    }

    [Fact]
    public void InitialFileDoesNotContainDownloadsRoot()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");

        // Act
        using SettingsStore store = new(filePath, "/data/torrents");

        // Assert
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
        doc.RootElement.TryGetProperty("DownloadsRoot", out _).ShouldBeFalse();
    }
}
