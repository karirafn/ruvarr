using System.Text.Json;

using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.SettingsStoreTests;

public sealed class SaveAsync : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public SaveAsync()
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
    public async Task UpdatesCurrentSettingsInMemory()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        using SettingsStore store = new(filePath);
        RuvarrSettings settings = new("http://localhost:8989", "key", "", "", "/downloads", "/episodes", "/movies");

        // Act
        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Assert
        store.Current.ShouldBe(settings);
    }

    [Fact]
    public async Task WritesSettingsToFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        using SettingsStore store = new(filePath);
        RuvarrSettings settings = new("http://localhost:8989", "key", "", "", "/downloads", "/episodes", "/movies");

        // Act
        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(filePath).ShouldBeTrue();
        string json = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
        JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("SonarrBaseAddress").GetString().ShouldBe("http://localhost:8989");
        doc.RootElement.GetProperty("SonarrApiKey").GetString().ShouldBe("key");
        doc.RootElement.GetProperty("DownloadsRootDirectory").GetString().ShouldBe("/downloads");
        doc.RootElement.GetProperty("EpisodeDownloadDirectory").GetString().ShouldBe("/episodes");
        doc.RootElement.GetProperty("MovieDownloadDirectory").GetString().ShouldBe("/movies");
    }

    [Fact]
    public async Task CreatesDirectoryIfItDoesNotExist()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "nested", "dir", "settings.json");
        using SettingsStore store = new(filePath);
        RuvarrSettings settings = new("http://localhost:8989", "key", "", "", "/downloads", "/episodes", "/movies");

        // Act
        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Assert
        File.Exists(filePath).ShouldBeTrue();
    }

    [Fact]
    public async Task OverwritesExistingFile()
    {
        // Arrange
        string filePath = Path.Combine(_tempDirectory, "settings.json");
        await File.WriteAllTextAsync(filePath, """{ "SonarrBaseAddress": "http://old:8989" }""", TestContext.Current.CancellationToken);
        using SettingsStore store = new(filePath);
        RuvarrSettings settings = new("http://new:8989", "", "", "", "", "", "");

        // Act
        await store.SaveAsync(settings, TestContext.Current.CancellationToken);

        // Assert
        string json = await File.ReadAllTextAsync(filePath, TestContext.Current.CancellationToken);
        JsonDocument doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("SonarrBaseAddress").GetString().ShouldBe("http://new:8989");
    }
}
