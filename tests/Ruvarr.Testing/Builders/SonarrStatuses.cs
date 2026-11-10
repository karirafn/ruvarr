using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Testing.Builders;

internal static class SonarrStatuses
{
    public static SonarrEpisodeStatuses NotTracked { get; } =
        SonarrEpisodeStatuses.From(new Dictionary<int, SonarrEpisodeStatus>());

    public static SonarrEpisodeStatuses Of(int tvdbId, SonarrEpisodeStatus status) =>
        SonarrEpisodeStatuses.From(new Dictionary<int, SonarrEpisodeStatus> { [tvdbId] = status });

    public static SonarrEpisodeStatuses Missing(int tvdbId) => Of(tvdbId, SonarrEpisodeStatus.Missing);
}