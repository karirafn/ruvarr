using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Domain;

namespace Ruvarr.SonarrWebhook.Commands.ReceiveSonarrWebhook;

// A delivery only nudges the affected programs' sync; the sync reads status from Sonarr's API.
internal sealed class ReceiveSonarrWebhookHandler(
    RuvarrDbContext dbContext,
    ProgramRefreshNotifier programRefreshNotifier,
    ISonarrMissingEpisodesCache missingEpisodesCache,
    IDomainEventBroadcaster broadcaster,
    ILogger<ReceiveSonarrWebhookHandler> logger) : IRequestHandler<ReceiveSonarrWebhookCommand>
{
    private static readonly HashSet<string> FileEventTypes = new(StringComparer.Ordinal)
    {
        "Download",
        "EpisodeFileDelete",
        "Rename",
    };

    public async Task<RuvarrResult> Handle(ReceiveSonarrWebhookCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.EventType is null
            || !FileEventTypes.Contains(request.EventType)
            || request.Series?.TvdbId is not int tvdbId)
        {
            logger.LogDebug("Ignoring Sonarr webhook delivery {EventType}", request.EventType);
            return RuvarrResult.Success;
        }

        var programs = await dbContext.Set<RuvProgram>()
            .AsNoTracking()
            .Where(p => p.Series != null && p.Series.TvdbId == tvdbId)
            .Select(p => new { p.RuvId, p.Name })
            .ToListAsync(cancellationToken);

        if (programs is [])
        {
            logger.LogDebug("Sonarr webhook {EventType} for TVDB series {TvdbId} matches no program", request.EventType, tvdbId);
            return RuvarrResult.Success;
        }

        // Cleared before queueing so the nudged sync cannot read a missing list from before the event.
        missingEpisodesCache.InvalidateMissingEpisodes();

        foreach (var program in programs)
        {
            programRefreshNotifier.PriorityEnqueue(program.RuvId, program.Name);
        }

        broadcaster.Publish(new QueueChangedEvent<ProgramRefreshQueueItemSummary>());

        logger.LogInformation(
            "Sonarr webhook {EventType} for TVDB series {TvdbId} queued {Count} program(s) for sync",
            request.EventType,
            tvdbId,
            programs.Count);

        return RuvarrResult.Success;
    }
}
