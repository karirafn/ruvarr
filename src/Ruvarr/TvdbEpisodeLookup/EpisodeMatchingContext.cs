using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup;

internal sealed record EpisodeMatchingContext(
    RuvProgram Program,
    SeriesData SeriesData,
    HashSet<int> MissingTvdbIds);
