using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup;

internal sealed record EpisodeMatchingContext(
    RuvProgram Program,
    SeriesData SeriesData,
    SonarrEpisodeStatuses SonarrStatuses)
{
    public IslTitleLookup IslTitles { get; } = new();

    public SeasonConstraint SeasonConstraint =>
        Program.ResolveSeasonConstraint(SeriesData.Episodes.Select(x => x.SeasonNumber).Where(x => x > 0).Distinct());
}
