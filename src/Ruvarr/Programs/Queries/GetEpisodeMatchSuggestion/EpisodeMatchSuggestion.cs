namespace Ruvarr.Programs.Queries.GetEpisodeMatchSuggestion;

public enum MatchSuggestionReason
{
    /// <summary>The title matches exactly one TVDB episode, outside the program's declared season(s).</summary>
    OutsideDeclaredSeason,

    /// <summary>A "Þáttur N" title points to episode N of the default season.</summary>
    EpisodeNumber,
}

/// <summary>The TVDB episode the manual match dialog pre-selects, and why.</summary>
public sealed record SuggestedEpisode(int TvdbId, MatchSuggestionReason Reason);

/// <summary>What the manual match dialog needs to present an unmatched RÚV episode.</summary>
/// <param name="Suggestion">The episode to pre-select; <c>null</c> when nothing can be suggested.</param>
/// <param name="DefaultSeason">The season the dialog offers first; <c>null</c> when the series has no regular seasons.</param>
/// <param name="DeclaredSeasons">The program's declared season(s), lowest first; empty when it declares none.</param>
public sealed record EpisodeMatchSuggestion(
    SuggestedEpisode? Suggestion,
    int? DefaultSeason,
    IReadOnlyList<int> DeclaredSeasons);
