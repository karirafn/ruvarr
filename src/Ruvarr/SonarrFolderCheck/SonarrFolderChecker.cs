using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Settings;

namespace Ruvarr.SonarrFolderCheck;

// Sonarr cannot be asked whether a folder exists: its filesystem API reports any path as a folder and
// its manual-import scan returns an empty list for a folder it cannot see. So the check writes a file
// Sonarr's scan lists only if Sonarr sees the same folder. The .mkv extension makes the scan list it.
internal sealed class SonarrFolderChecker(
    ISettingsStore settingsStore,
    ISonarrClient sonarr,
    SonarrFolderCheckHealth health,
    IDomainEventBroadcaster broadcaster,
    TimeProvider timeProvider,
    ILogger<SonarrFolderChecker> logger)
{
    internal const string ProbeFileName = "ruvarr-folder-check.mkv";

    public async Task<SonarrFolderCheckResult> CheckAsync(CancellationToken cancellationToken)
    {
        RuvarrSettings settings = settingsStore.Current;
        string folder = settings.ResolvedEpisodeDownloadDirectory;

        SonarrFolderCheckStatus status = await DetermineStatusAsync(settings, folder, cancellationToken);

        SonarrFolderCheckResult result = new(status, folder, timeProvider.GetUtcNow());
        health.Record(result);
        broadcaster.Publish(new SonarrFolderCheckedEvent());
        return result;
    }

    private async Task<SonarrFolderCheckStatus> DetermineStatusAsync(RuvarrSettings settings, string folder, CancellationToken cancellationToken)
    {
        if (!settings.IsSonarrConfigured)
        {
            logger.LogDebug("Skipping Sonarr folder check: Sonarr is not configured");
            return SonarrFolderCheckStatus.Unknown;
        }

        string probePath = Path.Join(folder, ProbeFileName);
        if (!TryWriteProbe(folder, probePath))
        {
            return SonarrFolderCheckStatus.Unknown;
        }

        try
        {
            Result<IReadOnlyList<ManualImportFile>> scan = await sonarr.GetManualImportsAsync(folder, seriesId: null, cancellationToken);
            if (scan.IsFailure)
            {
                logger.LogWarning(
                    "Sonarr folder check of {Folder} could not run: Sonarr's scan failed ({ErrorCode})",
                    folder, scan.Error.Code);
                return SonarrFolderCheckStatus.Unknown;
            }

            if (scan.FromResult().Any(f => f.Path.EndsWith(ProbeFileName, StringComparison.OrdinalIgnoreCase)))
            {
                return SonarrFolderCheckStatus.Passing;
            }

            logger.LogWarning(
                "Sonarr can't see the download folder {Folder}: its scan did not list the folder check file. Mount the downloads share at the same path in Ruvarr and Sonarr",
                folder);
            return SonarrFolderCheckStatus.Failing;
        }
        finally
        {
            DeleteProbe(probePath);
        }
    }

    private bool TryWriteProbe(string folder, string probePath)
    {
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(probePath, []);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Sonarr folder check could not write {ProbePath}", probePath);
            return false;
        }
    }

    private void DeleteProbe(string probePath)
    {
        try
        {
            File.Delete(probePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Sonarr folder check could not delete {ProbePath}; the next check overwrites it", probePath);
        }
    }
}
