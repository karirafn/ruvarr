using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Jobs;
using Ruvarr.Settings;
using Ruvarr.Settings.Commands.SaveSettings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.Commands.SaveSettings.SaveSettingsHandlerTests;

public sealed class Handle
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly ISchedulerFactory _schedulerFactory = Substitute.For<ISchedulerFactory>();
    private readonly IScheduler _scheduler = Substitute.For<IScheduler>();

    public Handle()
    {
        _store.Current.Returns(RuvarrSettings.Empty);
        _schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(_scheduler);
    }

    private SaveSettingsHandler CreateHandler() => new(_store, _schedulerFactory);

    [Fact]
    public async Task ReturnsErrorWhenSonarrBaseAddressIsRelativeUri()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("/relative/path", UriKind.Relative), "api-key", "", "", "tv", "movies", []);

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
        SaveSettingsCommand command = new(new Uri(url), "api-key", "", "", "tv", "movies", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.InvalidSonarrBaseAddress);
        await _store.DidNotReceive().SaveAsync(Arg.Any<RuvarrSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsErrorWhenEpisodeSubdirectoryIsAbsolute()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", "/absolute/path", "movies", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.EpisodeSubdirectoryAbsolute);
    }

    [Fact]
    public async Task ReturnsErrorWhenMovieSubdirectoryIsAbsolute()
    {
        // Arrange
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", "tv", "/absolute/path", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SettingsErrors.MovieSubdirectoryAbsolute);
    }

    [Fact]
    public async Task ReturnsSuccessWhenSubdirectoriesAreRelative()
    {
        // Arrange
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "test-api-key", "", "", "tv", "movies", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task PreservesExistingApiKeyWhenSentinelIsProvided()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(SonarrApiKey: "real-secret-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "****", "", "", "tv", "movies", []);

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
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "", "****", "", "tv", "movies", []);

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
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "", "", "****", "tv", "movies", []);

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
        SaveSettingsCommand command = new(baseUrl, "api-key", "", "", "tv", "movies", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        await _store.Received(1).SaveAsync(
            Arg.Is<RuvarrSettings>(s =>
                s.SonarrBaseAddress == "http://localhost:8989/" &&
                s.SonarrApiKey == "api-key" &&
                s.EpisodeDownloadDirectory == "tv" &&
                s.MovieDownloadDirectory == "movies"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggersRuvEpisodesSyncJobWhenSonarrBecomesConfigured()
    {
        // Arrange
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", "tv", "movies", []);

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        await _scheduler.Received(1).TriggerJob(
            Arg.Is<JobKey>(k => k.Name == nameof(RuvEpisodesSyncJob)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotTriggerRuvEpisodesSyncJobWhenSonarrWasAlreadyConfigured()
    {
        // Arrange
        _store.Current.Returns(new RuvarrSettings(SonarrBaseAddress: "http://localhost:8989", SonarrApiKey: "existing-key"));
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "api-key", "", "", "tv", "movies", []);

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        await _scheduler.DidNotReceive().TriggerJob(
            Arg.Any<JobKey>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotTriggerRuvEpisodesSyncJobWhenSonarrRemainsUnconfigured()
    {
        // Arrange
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("http://localhost:8989"), "", "", "", "tv", "movies", []);

        // Act
        await sut.Handle(command, CancellationToken.None);

        // Assert
        await _scheduler.DidNotReceive().TriggerJob(
            Arg.Any<JobKey>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotTriggerRuvEpisodesSyncJobOnValidationFailure()
    {
        // Arrange
        _store.Current.Returns(RuvarrSettings.Empty);
        SaveSettingsHandler sut = CreateHandler();
        SaveSettingsCommand command = new(new Uri("/relative/path", UriKind.Relative), "api-key", "", "", "tv", "movies", []);

        // Act
        RuvarrResult result = await sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        await _scheduler.DidNotReceive().TriggerJob(
            Arg.Any<JobKey>(),
            Arg.Any<CancellationToken>());
    }
}
