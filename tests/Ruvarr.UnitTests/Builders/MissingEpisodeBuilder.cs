using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.UnitTests.Builders;

internal sealed class MissingEpisodeBuilder
{
    private int _tvdbId = 1;

    public MissingEpisodeBuilder WithTvdbId(int tvdbId)
    {
        _tvdbId = tvdbId;
        return this;
    }

    public MissingEpisode Build() => new(
        Id: 1,
        SeriesId: 1,
        TvdbId: _tvdbId,
        EpisodeFileId: 0,
        SeasonNumber: 1,
        Episodenumber: 1,
        AbsoluteEpisodeNumber: 1,
        Title: "",
        Overview: "",
        FinaleType: "",
        AirDate: DateOnly.MinValue,
        AirDateUtc: DateTime.MinValue,
        Runtime: 0,
        HasFile: false,
        Monitored: true,
        UnverifiedSceneNumbering: false,
        LastSearchTime: DateTime.MinValue);
}
