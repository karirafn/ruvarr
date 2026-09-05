using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;

namespace Ruvarr.Downloads;

internal sealed class SonarrImporter(
    ISonarrClient sonarr,
    RuvarrDbContext dbContext,
    ILogger<SonarrImporter> logger)
{
    public async Task ImportAsync(
        DownloadQueueItem item,
        RuvarrSettings settings,
        string fileName,
        string completedPath,
        CancellationToken cancellationToken)
    {
        if (item.Episode.TvdbEpisodes.Count == 0)
        {
            logger.LogDebug("Episode is not matched with TVDB. Skipping Sonarr import");
            return;
        }

        // The outcomeWrite token is CancellationToken.None so that genuine failure
        // writes succeed even when the scheduler is concurrently shutting down — a cancelled save
        // would leave the failure unrecorded. Interruption (cancellation with no failure) takes a
        // different path: the OCE propagates out of ImportAsync unhandled and the catch-all carries
        // when (ex is not OperationCanceledException) and does not match.
        CancellationToken outcomeWrite = CancellationToken.None;

        try
        {
            IReadOnlyList<Series> sonarrSeries = await sonarr.GetSeriesAsync(cancellationToken);
            int? sonarrSeriesId = sonarrSeries
                .FirstOrDefault(s => s.TvdbId == item.Episode.Program.Series?.TvdbId)
                ?.Id;

            IReadOnlyList<ManualImportFile> manualImportFiles = await sonarr.GetManualImportsAsync(
                settings.ResolvedEpisodeDownloadDirectory,
                seriesId: null,
                cancellationToken);

            ManualImportFile? file = manualImportFiles
                .FirstOrDefault(x => x.Path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (file is null)
            {
                logger.LogWarning(
                    "Sonarr scan of {Folder} did not include {Filename}. Marking {Episode} failed",
                    settings.ResolvedEpisodeDownloadDirectory, fileName, item.Episode.ToString());
                item.MarkFailed("Sonarr scan did not include the file");
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            int? resolvedSeriesId = sonarrSeriesId ?? file.Series?.Id;
            if (resolvedSeriesId is null)
            {
                logger.LogWarning(
                    "Sonarr has no series for {Episode} (no TVDB-id match and no scan-matched series). Marking failed",
                    item.Episode.ToString());
                item.MarkFailed("Sonarr has no matching series");
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            IReadOnlyList<SonarrEpisode> sonarrEpisodes = await sonarr.GetEpisodesAsync(
                resolvedSeriesId.Value, cancellationToken);

            Dictionary<int, int> tvdbIdToSonarrEpisodeId = [];
            foreach (SonarrEpisode sonarrEpisode in sonarrEpisodes)
            {
                if (sonarrEpisode.TvdbId != 0)
                {
                    tvdbIdToSonarrEpisodeId.TryAdd(sonarrEpisode.TvdbId, sonarrEpisode.Id);
                }
            }

            if (!item.Episode.TryResolveSonarrEpisodeIds(tvdbIdToSonarrEpisodeId, out IReadOnlyList<int> episodeIds))
            {
                IEnumerable<int> unresolved = item.Episode.TvdbEpisodes
                    .Select(e => e.TvdbId)
                    .Where(id => !tvdbIdToSonarrEpisodeId.ContainsKey(id));
                logger.LogWarning(
                    "Sonarr series {SeriesId} is missing episodes for TVDB ids {UnresolvedTvdbIds}. Marking {Episode} failed",
                    resolvedSeriesId.Value, string.Join(", ", unresolved), item.Episode.ToString());
                item.MarkFailed("Sonarr is missing episodes");
                await dbContext.SaveChangesAsync(outcomeWrite);
                return;
            }

            ManualImportRequest request = new(
                Path: completedPath,
                SeriesId: resolvedSeriesId.Value,
                EpisodeIds: episodeIds,
                Quality: file.Quality,
                Languages: file.Languages,
                ReleaseGroup: "RUV");

            logger.LogInformation("Importing {Episode} into Sonarr", item.Episode.ToString());
            await sonarr.ManualImportFilesAsync([request], cancellationToken);
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent download items from getting stuck in Downloading state
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Sonarr import failed for {Episode}", item.Episode.ToString());
            item.MarkFailed("Sonarr import failed");
            await dbContext.SaveChangesAsync(outcomeWrite);
        }
    }
}
