using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr;

public sealed class SonarrEpisodeStatusLookupTests
{
    private const int SonarrSeriesId = 42;
    private const int TvdbSeriesId = 5000;

    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();

    public SonarrEpisodeStatusLookupTests()
    {
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>([SonarrSeriesBuilder.Build(SonarrSeriesId, TvdbSeriesId)]));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>(Array.Empty<MissingEpisode>()));
    }

    private static SonarrEpisode Episode(int tvdbId, bool hasFile, bool monitored = true) =>
        new(Id: tvdbId + 1000, SeriesId: SonarrSeriesId, TvdbId: tvdbId, SeasonNumber: 1, EpisodeNumber: 1, HasFile: hasFile, Monitored: monitored);

    private static MissingEpisode Missing(int tvdbId) =>
        new(
            Id: tvdbId + 1000,
            SeriesId: SonarrSeriesId,
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

    private void GivenEpisodes(params SonarrEpisode[] episodes) =>
        _sonarr.GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<SonarrEpisode>>(episodes));

    private void GivenMissing(params MissingEpisode[] missing) =>
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>(missing));

    [Fact]
    public async Task EpisodeWithFile_IsDownloaded()
    {
        GivenEpisodes(Episode(1, hasFile: true));

        IReadOnlyDictionary<int, SonarrEpisodeStatus> result =
            (await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None)).FromResult();

        result[1].ShouldBe(SonarrEpisodeStatus.Downloaded);
    }

    [Fact]
    public async Task EpisodeOnMissingList_IsMissing()
    {
        GivenEpisodes(Episode(1, hasFile: false));
        GivenMissing(Missing(1));

        IReadOnlyDictionary<int, SonarrEpisodeStatus> result =
            (await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None)).FromResult();

        result[1].ShouldBe(SonarrEpisodeStatus.Missing);
    }

    [Fact]
    public async Task EpisodeWithFile_IsDownloaded_EvenWhenStillOnMissingList()
    {
        GivenEpisodes(Episode(1, hasFile: true));
        GivenMissing(Missing(1));

        IReadOnlyDictionary<int, SonarrEpisodeStatus> result =
            (await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None)).FromResult();

        result[1].ShouldBe(SonarrEpisodeStatus.Downloaded);
    }

    [Fact]
    public async Task EpisodeWithoutFileAndNotOnMissingList_IsNotTracked()
    {
        GivenEpisodes(Episode(1, hasFile: false, monitored: false));

        IReadOnlyDictionary<int, SonarrEpisodeStatus> result =
            (await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None)).FromResult();

        result[1].ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Fact]
    public async Task SeriesNotInSonarr_SucceedsWithNoStatuses()
    {
        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> result =
            await _sonarr.GetEpisodeStatusesAsync(tvdbSeriesId: 9999, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.FromResult().ShouldBeEmpty();
        await _sonarr.DidNotReceive().GetEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithSeriesList_DoesNotReadSeriesAgain()
    {
        GivenEpisodes(Episode(1, hasFile: true));

        await _sonarr.GetEpisodeStatusesAsync(
            TvdbSeriesId,
            [SonarrSeriesBuilder.Build(SonarrSeriesId, TvdbSeriesId)],
            CancellationToken.None);

        await _sonarr.DidNotReceive().GetSeriesAsync(Arg.Any<CancellationToken>());
        await _sonarr.Received(1).GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenGetSeriesFails_ReturnsFailure()
    {
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(ApiClientErrors.RequestFailed));

        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> result =
            await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None);

        result.Error.ShouldBe(ApiClientErrors.RequestFailed);
    }

    [Fact]
    public async Task WhenGetEpisodesFails_ReturnsFailure()
    {
        _sonarr.GetEpisodesAsync(SonarrSeriesId, Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<SonarrEpisode>>(ApiClientErrors.RequestFailed));

        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> result =
            await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None);

        result.Error.ShouldBe(ApiClientErrors.RequestFailed);
    }

    [Fact]
    public async Task WhenGetMissingEpisodesFails_ReturnsFailure()
    {
        GivenEpisodes(Episode(1, hasFile: true));
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>(ApiClientErrors.RequestFailed));

        Result<IReadOnlyDictionary<int, SonarrEpisodeStatus>> result =
            await _sonarr.GetEpisodeStatusesAsync(TvdbSeriesId, CancellationToken.None);

        result.Error.ShouldBe(ApiClientErrors.RequestFailed);
    }
}