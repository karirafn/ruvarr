using Ruvarr.Abstractions;

namespace Ruvarr.Settings.Commands.SaveSettings;

internal sealed class SaveSettingsHandler(ISettingsStore store) : IRequestHandler<SaveSettingsCommand>
{
    private const string ApiKeySentinel = "****";

    public async Task<RuvarrResult> Handle(SaveSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!command.SonarrBaseAddress.IsAbsoluteUri)
        {
            return SettingsErrors.InvalidSonarrBaseAddress;
        }

        if (command.SonarrBaseAddress.Scheme != Uri.UriSchemeHttp &&
            command.SonarrBaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            return SettingsErrors.InvalidSonarrBaseAddress;
        }

        if (!Directory.Exists(command.DownloadsRootDirectory))
        {
            return SettingsErrors.DownloadsRootDirectoryNotFound;
        }

        string resolvedRoot = Path.GetFullPath(command.DownloadsRootDirectory);

        string resolvedEpisodeDir = Path.GetFullPath(command.EpisodeDownloadDirectory);
        if (!resolvedEpisodeDir.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && resolvedEpisodeDir != resolvedRoot)
        {
            return SettingsErrors.EpisodeDownloadDirectoryNotUnderRoot;
        }

        Directory.CreateDirectory(resolvedEpisodeDir);

        string resolvedMovieDir = Path.GetFullPath(command.MovieDownloadDirectory);
        if (!resolvedMovieDir.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && resolvedMovieDir != resolvedRoot)
        {
            return SettingsErrors.MovieDownloadDirectoryNotUnderRoot;
        }

        Directory.CreateDirectory(resolvedMovieDir);

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
            command.DownloadsRootDirectory,
            command.EpisodeDownloadDirectory,
            command.MovieDownloadDirectory)
        {
            IgnoredChannels = command.IgnoredChannels
        };

        await store.SaveAsync(settings, cancellationToken);

        return RuvarrResult.Success;
    }
}
