using Ruvarr.Abstractions;

namespace Ruvarr.Settings.Commands.SaveSettings;

internal sealed class SaveSettingsHandler(ISettingsStore store) : IRequestHandler<SaveSettingsCommand>
{
    public async Task<RuvarrResult> Handle(SaveSettingsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SonarrBaseUrl is not null && !command.SonarrBaseUrl.IsAbsoluteUri)
        {
            return SettingsErrors.InvalidSonarrBaseUrl;
        }

        if (command.SonarrBaseUrl is not null &&
            command.SonarrBaseUrl.Scheme != Uri.UriSchemeHttp &&
            command.SonarrBaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            return SettingsErrors.InvalidSonarrBaseUrl;
        }

        if (command.EpisodeDownloadDirectory is not null &&
            !Directory.Exists(command.EpisodeDownloadDirectory))
        {
            return SettingsErrors.EpisodeDownloadDirectoryNotFound;
        }

        if (command.MovieDownloadDirectory is not null &&
            !Directory.Exists(command.MovieDownloadDirectory))
        {
            return SettingsErrors.MovieDownloadDirectoryNotFound;
        }

        RuvarrSettings settings = new(
            command.SonarrBaseUrl?.ToString(),
            command.SonarrApiKey,
            command.EpisodeDownloadDirectory,
            command.MovieDownloadDirectory);

        await store.SaveAsync(settings, cancellationToken);

        return RuvarrResult.Success;
    }
}
