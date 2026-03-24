using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Settings;
using Ruvarr.Settings.Queries.GetSettings;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.Queries.GetSettings.GetSettingsHandlerTests;

public sealed class Handle
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();

    private GetSettingsHandler CreateHandler() => new(_store);

    [Fact]
    public async Task ReturnsCurrentSettingsFromStore()
    {
        // Arrange
        RuvarrSettings stored = new("http://localhost:8989", "key", "tvdb-key", "tmdb-key", "episodes", "movies");
        _store.Current.Returns(stored);
        GetSettingsHandler sut = CreateHandler();

        // Act
        Result<RuvarrSettings> result = await sut.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RuvarrSettings settings = result.FromResult();
        settings.SonarrBaseAddress.ShouldBe("http://localhost:8989");
        settings.SonarrApiKey.ShouldBe("****");
        settings.TvdbApiKey.ShouldBe("****");
        settings.TmdbApiKey.ShouldBe("****");
        settings.EpisodeDownloadDirectory.ShouldBe("episodes");
        settings.MovieDownloadDirectory.ShouldBe("movies");
    }

    [Fact]
    public async Task DoesNotMaskApiKeysWhenEmpty()
    {
        // Arrange
        RuvarrSettings stored = new("http://localhost:8989", "", "", "", "episodes", "movies");
        _store.Current.Returns(stored);
        GetSettingsHandler sut = CreateHandler();

        // Act
        Result<RuvarrSettings> result = await sut.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RuvarrSettings settings = result.FromResult();
        settings.SonarrApiKey.ShouldBeEmpty();
        settings.TvdbApiKey.ShouldBeEmpty();
        settings.TmdbApiKey.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsEmptySettingsWhenStoreHasNoValues()
    {
        // Arrange
        _store.Current.Returns(RuvarrSettings.Empty);
        GetSettingsHandler sut = CreateHandler();

        // Act
        Result<RuvarrSettings> result = await sut.Handle(new GetSettingsQuery(), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RuvarrSettings settings = result.FromResult();
        settings.ShouldBe(RuvarrSettings.Empty);
    }
}
