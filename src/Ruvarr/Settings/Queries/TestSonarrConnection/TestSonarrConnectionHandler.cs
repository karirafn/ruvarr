using Ruvarr.Abstractions;

namespace Ruvarr.Settings.Queries.TestSonarrConnection;

internal sealed class TestSonarrConnectionHandler(
    IHttpClientFactory httpClientFactory,
    ISettingsStore store) : IRequestHandler<TestSonarrConnectionQuery>
{
    private const string SonarrStatusEndpoint = "/api/v3/system/status";
    private const string ErrorCode = "Settings.SonarrConnectionFailed";

    public async Task<RuvarrResult> Handle(TestSonarrConnectionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(request.SonarrBaseAddress, UriKind.Absolute, out Uri? baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            return SettingsErrors.InvalidSonarrBaseAddress;
        }

        string apiKey = request.UseStoredApiKey
            ? store.Current.SonarrApiKey
            : request.SonarrApiKey;

        using HttpClient client = httpClientFactory.CreateClient();
        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        try
        {
            using HttpResponseMessage response = await client.GetAsync(SonarrStatusEndpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            return RuvarrResult.Success;
        }
        catch (HttpRequestException ex)
        {
            string message = ex.StatusCode is not null
                ? $"Sonarr responded with status code {(int)ex.StatusCode}."
                : $"Could not connect to Sonarr: {ex.Message}";

            return new RuvarrError(ErrorCode, message);
        }
    }
}
