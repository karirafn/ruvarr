using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Settings;
using Ruvarr.Settings.Commands.SaveSettings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.Commands.SaveSettings.SaveSettingsHandlerTests;

public sealed class Handle : IDisposable
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public Handle()
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

    private SaveSettingsHandler CreateHandler() => new(_store);

    [Fact]
    public async Task ReturnsErrorWhenSonarrBaseAddressIsRelativeUri()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("/relative/path", UriKind.Relative), "api-key", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.InvalidSonarrBaseAddress);
        await _store.DidNotReceive().SaveAsync(Arg.Any<RuvarrSettings>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public async Task ReturnsErrorWhenSonarrBaseAddressSchemeIsNotHttpOrHttps(string url)
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri(url), "api-key", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.InvalidSonarrBaseAddress);
        await _store.DidNotReceive().SaveAsync(Arg.Any<RuvarrSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsErrorWhenDownloadsRootDirectoryDoesNotExist()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "/nonexistent/directory", _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.DownloadsRootDirectoryNotFound);
    }

    [Fact]
    public async Task ReturnsErrorWhenEpisodeDownloadDirectoryDoesNotExist()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", _tempDirectory, "/nonexistent/directory", _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.EpisodeDownloadDirectoryNotFound);
    }

    [Fact]
    public async Task ReturnsErrorWhenMovieDownloadDirectoryDoesNotExist()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", _tempDirectory, _tempDirectory, "/nonexistent/directory", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.MovieDownloadDirectoryNotFound);
    }

    [Fact]
    public async Task PreservesExistingApiKeyWhenSentinelIsProvided()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(SonarrApiKey: "real-secret-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "****", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s => s.SonarrApiKey == "real-secret-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavesSettingsWhenAllFieldsAreValid()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        Uri baseUrl = new("http://localhost:8989");
        SaveSettingsCommand command = new(baseUrl, "api-key", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s =>
                s.SonarrBaseAddress == "http://localhost:8989/" &&
                s.SonarrApiKey == "api-key" &&
                s.DownloadsRootDirectory == _tempDirectory &&
                s.EpisodeDownloadDirectory == _tempDirectory &&
                s.MovieDownloadDirectory == _tempDirectory),
            Arg.Any<CancellationToken>());
    }
}
