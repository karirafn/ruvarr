using Ruvarr.Infrastructure.Tvdb.Models;

namespace Ruvarr.Testing.Builders;

internal sealed class DatumBuilder
{
    private string _name = "Test Series";
    private string _tvdbId = "1000";
    private string _type = "series";
    private string _slug = "test-series";
    private IReadOnlyDictionary<string, string> _translations = new Dictionary<string, string>();

    public DatumBuilder WithName(string name) { _name = name; return this; }

    public DatumBuilder WithTvdbId(string tvdbId) { _tvdbId = tvdbId; return this; }

    public DatumBuilder WithType(string type) { _type = type; return this; }

    public DatumBuilder WithSlug(string slug) { _slug = slug; return this; }

    public DatumBuilder WithTranslations(IReadOnlyDictionary<string, string> translations)
    {
        _translations = translations;
        return this;
    }

    public Datum Build() => new(
        ObjectId: "series-1000",
        Aliases: [],
        Country: "IS",
        Id: _tvdbId,
        ImageUrl: new Uri("https://image.com"),
        Name: _name,
        FirstAirDate: "2024-01-01",
        Overview: "",
        PrimaryLanguage: "isl",
        PrimaryType: "series",
        Status: "Continuing",
        Type: _type,
        TvdbId: _tvdbId,
        Year: "2024",
        Slug: _slug,
        Overviews: new Dictionary<string, string>(),
        Translations: _translations,
        Network: "",
        RemoteIds: []);
}
