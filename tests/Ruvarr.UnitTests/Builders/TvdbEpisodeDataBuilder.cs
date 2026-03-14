using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.UnitTests.Builders;

internal sealed class TvdbEpisodeDataBuilder
{
    private int _id = 100;
    private readonly int _seriesId = 1000;
    private string _name = "Test Episode";
    private IReadOnlyList<string> _nameTranslations = [];
    private int _number = 1;
    private int _seasonNumber = 1;

    public TvdbEpisodeDataBuilder WithId(int id) { _id = id; return this; }

    public TvdbEpisodeDataBuilder WithName(string name) { _name = name; return this; }

    public TvdbEpisodeDataBuilder WithNameTranslations(params string[] translations)
    {
        _nameTranslations = translations;
        return this;
    }

    public TvdbEpisodeDataBuilder WithNumber(int number) { _number = number; return this; }

    public TvdbEpisodeDataBuilder WithSeasonNumber(int season) { _seasonNumber = season; return this; }

    public Episode Build() => new(
        Id: _id,
        SeriesId: _seriesId,
        Name: _name,
        Overview: "",
        Aired: "2024-01-01",
        Runtime: null,
        NameTranslations: _nameTranslations,
        OverviewTranslations: [],
        Image: new Uri("http://image.com"),
        ImageType: null,
        IsMovie: 0,
        Number: _number,
        AbsoluteNumber: _number,
        SeasonNumber: _seasonNumber,
        LastUpdated: "2024-01-01",
        FinaleType: null,
        Year: "2024",
        AirsAfterSeason: 0,
        AirsBeforeSeason: 0,
        AirsBeforeEpisode: 0);
}
