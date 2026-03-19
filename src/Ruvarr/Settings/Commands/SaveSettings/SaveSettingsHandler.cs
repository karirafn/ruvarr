using Ruvarr.Abstractions;

namespace Ruvarr.Settings.Commands.SaveSettings;

internal sealed class SaveSettingsHandler(ISettingsStore store) : IRequestHandler<SaveSettingsCommand>
{
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

        if (!Directory.Exists(command.EpisodeDownloadDirectory))
        {
            return SettingsErrors.EpisodeDownloadDirectoryNotFound;
        }

        if (!Directory.Exists(command.MovieDownloadDirectory))
        {
            return SettingsErrors.MovieDownloadDirectoryNotFound;
        }

        RuvarrSettings settings = new(
            command.SonarrBaseAddress.ToString(),
            command.SonarrApiKey,
            command.DownloadsRootDirectory,
            command.EpisodeDownloadDirectory,
            command.MovieDownloadDirectory,
            command.FfmpegPath)
        {
            IgnoredChannels = command.IgnoredChannels
        };

        await store.SaveAsync(settings, cancellationToken);

        return RuvarrResult.Success;
    }
}
