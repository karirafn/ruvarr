using Ruvarr.Downloads.Queries.GetDownloadQueue;

namespace Ruvarr.Downloads;

/// <summary>Where keyboard focus goes on the Downloads page after a row action re-queries the queue.</summary>
internal abstract record DownloadsFocusTarget
{
    private DownloadsFocusTarget()
    {
    }

    public sealed record Heading : DownloadsFocusTarget;

    public sealed record RowButton(string EpisodeRuvId, DownloadRowAction Button) : DownloadsFocusTarget;

    /// <param name="items">The queue as re-queried after the action.</param>
    /// <param name="episodeRuvId">The episode whose row started the action.</param>
    /// <param name="action">The button that started the action.</param>
    /// <param name="index">The row's position before the action.</param>
    public static DownloadsFocusTarget AfterAction(
        IReadOnlyList<DownloadQueueItemSummary> items,
        string episodeRuvId,
        DownloadRowAction action,
        int index)
    {
        if (items.Any(x => x.EpisodeRuvId == episodeRuvId && x.HasRowActions))
        {
            return new RowButton(episodeRuvId, action);
        }

        int start = Math.Clamp(index, 0, items.Count);
        DownloadQueueItemSummary? target = items.Skip(start).FirstOrDefault(x => x.HasRowActions)
            ?? items.Take(start).LastOrDefault(x => x.HasRowActions);

        // Retry rather than Dismiss, so a repeated key press cannot dismiss the next item too.
        return target is null
            ? new Heading()
            : new RowButton(target.EpisodeRuvId, DownloadRowAction.Retry);
    }
}
