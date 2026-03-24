using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Jobs;

namespace Ruvarr.Settings.Commands.SaveSettings;

internal sealed class SaveSettingsHandler(ISettingsStore store, ISchedulerFactory schedulerFactory) : IRequestHandler<SaveSettingsCommand>
{
    private const string ApiKeySentinel = "****";

    public async Task<RuvarrResult> Handle(SaveSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        bool wasSonarrConfigured = store.Current.IsSonarrConfigured;

        if (!command.SonarrBaseAddress.IsAbsoluteUri)
        {
            return SettingsErrors.InvalidSonarrBaseAddress;
        }

        if (command.SonarrBaseAddress.Scheme != Uri.UriSchemeHttp &&
            command.SonarrBaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            return SettingsErrors.InvalidSonarrBaseAddress;
        }

        if (string.IsNullOrWhiteSpace(command.EpisodeDownloadDirectory))
        {
            return SettingsErrors.EpisodeSubdirectoryEmpty;
        }

        if (string.IsNullOrWhiteSpace(command.MovieDownloadDirectory))
        {
            return SettingsErrors.MovieSubdirectoryEmpty;
        }

        if (Path.IsPathRooted(command.EpisodeDownloadDirectory))
        {
            return SettingsErrors.EpisodeSubdirectoryAbsolute;
        }

        if (Path.IsPathRooted(command.MovieDownloadDirectory))
        {
            return SettingsErrors.MovieSubdirectoryAbsolute;
        }

        string normalizedRoot = Path.GetFullPath(RuvarrSettings.DownloadsRoot + Path.DirectorySeparatorChar);
        string normalizedEpisodePath = Path.GetFullPath(Path.Join(RuvarrSettings.DownloadsRoot, command.EpisodeDownloadDirectory));
        if (!normalizedEpisodePath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            return SettingsErrors.EpisodeSubdirectoryTraversal;
        }

        string normalizedMoviePath = Path.GetFullPath(Path.Join(RuvarrSettings.DownloadsRoot, command.MovieDownloadDirectory));
        if (!normalizedMoviePath.StartsWith(normalizedRoot, StringComparison.Ordinal))
        {
            return SettingsErrors.MovieSubdirectoryTraversal;
        }

        string sonarrApiKey = command.SonarrApiKey == ApiKeySentinel
            ? store.Current.SonarrApiKey
            : command.SonarrApiKey;

        string tvdbApiKey = command.TvdbApiKey == ApiKeySentinel
            ? store.Current.TvdbApiKey
            : command.TvdbApiKey;

        string tmdbApiKey = command.TmdbApiKey == ApiKeySentinel
            ? store.Current.TmdbApiKey
            : command.TmdbApiKey;

        RuvarrSettings settings = new(
            command.SonarrBaseAddress.ToString(),
            sonarrApiKey,
            tvdbApiKey,
            tmdbApiKey,
            command.EpisodeDownloadDirectory,
            command.MovieDownloadDirectory)
        {
            IgnoredChannels = command.IgnoredChannels
        };

        await store.SaveAsync(settings, cancellationToken);

        if (!wasSonarrConfigured && settings.IsSonarrConfigured)
        {
            IScheduler scheduler = await schedulerFactory.GetScheduler(cancellationToken);
            await scheduler.TriggerJob(new JobKey(nameof(RuvEpisodesSyncJob)), cancellationToken);
        }

        return RuvarrResult.Success;
    }
}
