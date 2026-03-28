using NSubstitute;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr;

public sealed class SonarrClientExtensionsTests
{
    private static MissingEpisode CreateMissingEpisode(int tvdbId) =>
        new(
            Id: 1,
            SeriesId: 10,
            TvdbId: tvdbId,
            EpisodeFileId: 0,
            SeasonNumber: 1,
            Episodenumber: 1,
            AbsoluteEpisodeNumber: 1,
            Title: "Test Episode",
            Overview: "Overview",
            FinaleType: "",
            AirDate: new DateOnly(2025, 1, 1),
            AirDateUtc: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Runtime: 30,
            HasFile: false,
            Monitored: true,
            UnverifiedSceneNumbering: false,
            LastSearchTime: DateTime.UtcNow);

    [Fact]
    public async Task GetMissingTvdbIdsAsync_ReturnsHashSetOfTvdbIds()
    {
        // Arrange
        ISonarrClient sonarr = Substitute.For<ISonarrClient>();
        IReadOnlyCollection<MissingEpisode> missingEpisodes =
        [
            CreateMissingEpisode(100),
            CreateMissingEpisode(200),
            CreateMissingEpisode(300)
        ];
        sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(missingEpisodes);

        // Act
        HashSet<int> result = await sonarr.GetMissingTvdbIdsAsync(CancellationToken.None);

        // Assert
        result.ShouldBe(new HashSet<int> { 100, 200, 300 });
    }

    [Fact]
    public async Task GetMissingTvdbIdsAsync_WithNoMissingEpisodes_ReturnsEmptySet()
    {
        // Arrange
        ISonarrClient sonarr = Substitute.For<ISonarrClient>();
        sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        // Act
        HashSet<int> result = await sonarr.GetMissingTvdbIdsAsync(CancellationToken.None);

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetMissingTvdbIdsAsync_WithDuplicateTvdbIds_ReturnsDistinctSet()
    {
        // Arrange
        ISonarrClient sonarr = Substitute.For<ISonarrClient>();
        IReadOnlyCollection<MissingEpisode> missingEpisodes =
        [
            CreateMissingEpisode(100),
            CreateMissingEpisode(100)
        ];
        sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(missingEpisodes);

        // Act
        HashSet<int> result = await sonarr.GetMissingTvdbIdsAsync(CancellationToken.None);

        // Assert
        result.Count.ShouldBe(1);
        result.ShouldContain(100);
    }

    [Fact]
    public async Task GetMissingTvdbIdsAsync_PassesCancellationToken()
    {
        // Arrange
        ISonarrClient sonarr = Substitute.For<ISonarrClient>();
        sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);
        using CancellationTokenSource cts = new();

        // Act
        await sonarr.GetMissingTvdbIdsAsync(cts.Token);

        // Assert
        await sonarr.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), cts.Token);
    }
}
