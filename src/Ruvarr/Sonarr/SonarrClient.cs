using System.Collections.Specialized;
using System.Net.Http.Json;
using System.Web;

using Ruvarr.Sonarr.Models;

namespace Ruvarr.Sonarr;

internal sealed class SonarrClient(HttpClient httpClient) : ISonarrClient
{
    public async Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("pageSize", $"{pageSize}");

        string path = $"api/v3/wanted/missing?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        MissingEpisodesResponse? response = await httpClient.GetFromJsonAsync<MissingEpisodesResponse>(path)
            .ConfigureAwait(false);

        return response?.Records ?? [];
    }
}