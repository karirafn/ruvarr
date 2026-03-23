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
        SaveSettingsCommand command = new(new Uri("/relative/path", UriKind.Relative), "api-key", "", "", _tempDirectory, _tempDirectory, _tempDirectory, []);

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
        SaveSettingsCommand command = new(new Uri(url), "api-key", "", "", _tempDirectory, _tempDirectory, _tempDirectory, []);

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
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", "/nonexistent/directory", _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.DownloadsRootDirectoryNotFound);
    }

    [Fact]
    public async Task CreatesEpisodeDownloadDirectoryWhenUnderRootButMissing()
    {
        // Arrange
        string episodeDir = Path.Combine(_tempDirectory, "episodes");
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", _tempDirectory, episodeDir, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Directory.Exists(episodeDir).ShouldBeTrue();
    }

    [Fact]
    public async Task CreatesMovieDownloadDirectoryWhenUnderRootButMissing()
    {
        // Arrange
        string movieDir = Path.Combine(_tempDirectory, "movies");
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", _tempDirectory, _tempDirectory, movieDir, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Directory.Exists(movieDir).ShouldBeTrue();
    }

    [Fact]
    public async Task CreatesBothDirectoriesWhenUnderRootButMissing()
    {
        // Arrange
        string episodeDir = Path.Combine(_tempDirectory, "episodes");
        string movieDir = Path.Combine(_tempDirectory, "movies");
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", _tempDirectory, episodeDir, movieDir, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        Directory.Exists(episodeDir).ShouldBeTrue();
        Directory.Exists(movieDir).ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsErrorWhenEpisodeDirectoryIsOutsideRoot()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sibling = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(sibling);
            SaveSettingsHandler sut = CreateHandler();
            SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "test-api-key", "", "", root, sibling, root, []);

            // Act
            RuvarrResult result = await sut.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(SettingsErrors.EpisodeDownloadDirectoryNotUnderRoot);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(sibling)) Directory.Delete(sibling, true);
        }
    }

    [Fact]
    public async Task ReturnsErrorWhenMovieDirectoryIsOutsideRoot()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sibling = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(sibling);
            SaveSettingsHandler sut = CreateHandler();
            SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "test-api-key", "", "", root, root, sibling, []);

            // Act
            RuvarrResult result = await sut.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(SettingsErrors.MovieDownloadDirectoryNotUnderRoot);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(sibling)) Directory.Delete(sibling, true);
        }
    }

    [Fact]
    public async Task ReturnsSuccessWhenDirectoriesAreUnderRoot()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string episodeDir = Path.Combine(root, "episodes");
        string movieDir = Path.Combine(root, "movies");
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(episodeDir);
            Directory.CreateDirectory(movieDir);
            _store.Current.Returns(RuvarrSettings.Empty);
            SaveSettingsHandler sut = CreateHandler();
            SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "test-api-key", "", "", root, episodeDir, movieDir, []);

            // Act
            RuvarrResult result = await sut.Handle(command, CancellationToken.None);

            // Assert
            result.IsSuccess.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ReturnsErrorWhenEpisodeDirectoryUsesTraversal()
    {
        // Arrange
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string sibling = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        try
        {
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(sibling);
            string traversalPath = Path.Combine(root, "..", Path.GetFileName(sibling));
            SaveSettingsHandler sut = CreateHandler();
            SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "test-api-key", "", "", root, traversalPath, root, []);

            // Act
            RuvarrResult result = await sut.Handle(command, CancellationToken.None);

            // Assert
            result.IsFailure.ShouldBeTrue();
            result.Error.ShouldBe(SettingsErrors.EpisodeDownloadDirectoryNotUnderRoot);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(sibling)) Directory.Delete(sibling, true);
        }
    }

    [Fact]
    public async Task PreservesExistingApiKeyWhenSentinelIsProvided()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(SonarrApiKey: "real-secret-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "****", "", "", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s => s.SonarrApiKey == "real-secret-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreservesExistingTvdbApiKeyWhenSentinelIsProvided()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(TvdbApiKey: "real-tvdb-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "", "****", "", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s => s.TvdbApiKey == "real-tvdb-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PreservesExistingTmdbApiKeyWhenSentinelIsProvided()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(TmdbApiKey: "real-tmdb-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "", "", "****", _tempDirectory, _tempDirectory, _tempDirectory, []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s => s.TmdbApiKey == "real-tmdb-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SavesSettingsWhenAllFieldsAreValid()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        Uri baseUrl = new("http://localhost:8989");
        SaveSettingsCommand command = new(baseUrl, "api-key", "", "", _tempDirectory, _tempDirectory, _tempDirectory, []);

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
