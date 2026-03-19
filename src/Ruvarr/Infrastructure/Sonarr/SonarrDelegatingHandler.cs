using Ruvarr.Settings;

namespace Ruvarr.Infrastructure.Sonarr;

internal sealed class SonarrDelegatingHandler(ISettingsStore settingsStore) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        RuvarrSettings settings = settingsStore.Current;

        if (!string.IsNullOrEmpty(settings.SonarrBaseAddress) &&
            Uri.TryCreate(settings.SonarrBaseAddress, UriKind.Absolute, out Uri? baseUri) &&
            request.RequestUri is not null)
        {
            request.RequestUri = new Uri(baseUri, request.RequestUri.PathAndQuery);
        }

        if (!string.IsNullOrEmpty(settings.SonarrApiKey))
        {
            request.Headers.Remove("X-Api-Key");
            request.Headers.Add("X-Api-Key", settings.SonarrApiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
