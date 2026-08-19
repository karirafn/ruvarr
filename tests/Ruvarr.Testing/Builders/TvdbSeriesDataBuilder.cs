using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Testing.Builders;

internal sealed class TvdbSeriesDataBuilder
{
    private int _id = 1000;
    private string _name = "Test Series";
    private IReadOnlyList<Episode> _episodes = [];

    public TvdbSeriesDataBuilder WithId(int id) { _id = id; return this; }

    public TvdbSeriesDataBuilder WithName(string name) { _name = name; return this; }

    public TvdbSeriesDataBuilder WithEpisodes(params Episode[] episodes)
    {
        _episodes = episodes;
        return this;
    }

    public SeriesData Build()
    {
        Series series = new(
            Id: _id,
            Name: _name,
            Slug: "test-series",
            Image: new Uri("https://image.com"),
            NameTranslations: [],
            OverviewTranslations: [],
            Episodes: [],
            Aliases: [],
            FirstAired: "2024-01-01",
            LastAired: "2024-01-01",
            NextAired: "",
            Score: 0,
            Status: new Status(1, "Continuing", "series", true),
            OriginalCountry: "IS",
            OriginalLanguage: "isl",
            IsOrderRandomized: false,
            LastUpdated: "2024-01-01",
            AverageRuntime: null,
            Overview: "",
            Year: "2024");

        return new SeriesData(series, _episodes);
    }
}
