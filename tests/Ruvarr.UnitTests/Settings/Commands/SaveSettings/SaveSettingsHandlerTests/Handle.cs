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
    public async Task ReturnsErrorWhenSonarrBaseUrlIsRelativeUri()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("/relative/path", UriKind.Relative), null, null, null);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.InvalidSonarrBaseUrl);
        await _store.DidNotReceive().SaveAsync(Arg.Any<RuvarrSettings>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://example.com")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1054:URI-like parameters should not be strings")]
    public async Task ReturnsErrorWhenSonarrBaseUrlSchemeIsNotHttpOrHttps(string url)
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri(url), null, null, null);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.InvalidSonarrBaseUrl);
        await _store.DidNotReceive().SaveAsync(Arg.Any<RuvarrSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsErrorWhenEpisodeDownloadDirectoryDoesNotExist()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(null, null, "/nonexistent/directory", null);

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
        SaveSettingsCommand command = new(null, null, null, "/nonexistent/directory");

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.MovieDownloadDirectoryNotFound);
    }

    [Fact]
    public async Task SavesSettingsWhenAllFieldsAreValid()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        Uri baseUrl = new("http://localhost:8989");
        SaveSettingsCommand command = new(baseUrl, "api-key", _tempDirectory, _tempDirectory);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s =>
                s.SonarrBaseUrl == "http://localhost:8989/" &&
                s.SonarrApiKey == "api-key" &&
                s.EpisodeDownloadDirectory == _tempDirectory &&
                s.MovieDownloadDirectory == _tempDirectory),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavesSettingsWhenAllFieldsAreNull()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(null, null, null, null);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s =>
                s.SonarrBaseUrl == null &&
                s.SonarrApiKey == null &&
                s.EpisodeDownloadDirectory == null &&
                s.MovieDownloadDirectory == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SkipsUrlValidationWhenSonarrBaseUrlIsNull()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(null, "some-key", null, null);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SkipsDirectoryValidationWhenDirectoriesAreNull()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), null, null, null);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}
