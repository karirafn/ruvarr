using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Testing.Builders;

internal static class SonarrSeriesBuilder
{
    public static Series Build(int id, int tvdbId, bool monitored = true, IReadOnlyList<Season>? seasons = null) => new(
        Title: "Test Series",
        SortTitle: "test series",
        Status: "continuing",
        Ended: false,
        Overview: "",
        Airtime: new TimeOnly(20, 0),
        Originallanguage: new Originallanguage(1, "English"),
        Year: 2024,
        Path: "/tv/test",
        QualityProfileId: 1,
        SeasonFolder: true,
        Monitored: monitored,
        MonitorNewItems: "all",
        UseScheneNumbering: false,
        Runtime: 30,
        TvdbId: tvdbId,
        TvRageId: 0,
        TvMazeId: 0,
        TmdbId: 0,
        SeriesType: "standard",
        CleanTitle: "testseries",
        ImdbId: "",
        TitleSlug: "test-series",
        Certification: "",
        FirstAired: DateTime.UtcNow,
        LastAired: DateTime.UtcNow,
        Added: DateTime.UtcNow,
        Images: [],
        Seasons: seasons ?? [],
        Genres: [],
        Ratings: new Ratings(0, 0),
        LanguageProfileId: 1,
        Id: id);
}