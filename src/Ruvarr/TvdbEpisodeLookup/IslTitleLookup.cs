using System.Collections.Concurrent;

using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.TvdbEpisodeLookup;

/// <summary>A TVDB episode paired with its Icelandic (<c>isl</c>) title translation.</summary>
internal sealed record IslTitle(Episode Episode, string Name);

/// <summary>
/// The Icelandic title translations fetched during one TVDB episode lookup pass, so the match re-check and
/// translation matching share one TVDB call per episode.
/// </summary>
internal sealed class IslTitleLookup
{
    private const int MaxDegreeOfParallelism = 3;

    private readonly ConcurrentDictionary<int, string?> _names = new();

    /// <summary>
    /// The titles of <paramref name="episodes"/> that have an Icelandic translation, in the order given,
    /// fetching only those not fetched earlier in the pass.
    /// </summary>
    public async Task<IReadOnlyList<IslTitle>> GetAsync(
        ITvdbClient tvdb,
        IReadOnlyList<Episode> episodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tvdb);
        ArgumentNullException.ThrowIfNull(episodes);

        await Parallel.ForEachAsync(
            episodes.Where(x => x.NameTranslations.Contains("isl") && !_names.ContainsKey(x.Id)),
            new ParallelOptions { MaxDegreeOfParallelism = MaxDegreeOfParallelism, CancellationToken = cancellationToken },
            async (episode, ct) =>
            {
                EpisodeTranslation? translation = await tvdb.GetEpisodeTranslationAsync(episode.Id, cancellationToken: ct);
                _names[episode.Id] = translation?.Name;
            });

        return [.. episodes
            .Select(x => _names.TryGetValue(x.Id, out string? name) && name is not null ? new IslTitle(x, name) : null)
            .OfType<IslTitle>()];
    }

    public static IReadOnlyList<Episode> CandidatesFor(RuvEpisode episode, IReadOnlyList<IslTitle> titles)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(titles);

        return [.. titles.Where(x => episode.IsMatch(x.Name)).Select(x => x.Episode)];
    }
}
