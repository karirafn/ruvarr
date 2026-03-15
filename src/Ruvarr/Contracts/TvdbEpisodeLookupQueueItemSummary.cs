namespace Ruvarr.Contracts;

public sealed record TvdbEpisodeLookupQueueItemSummary(
    int RuvId,
    string ProgramName,
    TvdbEpisodeLookupStatus Status) : IQueueItemSummary
{
    public bool IsProcessing => Status == TvdbEpisodeLookupStatus.Processing;
}