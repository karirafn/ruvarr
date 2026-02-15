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

    public Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files)
    {
        ManualImportCommand command = new(files);
        return httpClient.PostAsJsonAsync("api/v3/command", command);
    }

    public async Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("folder", folder);

        string path = $"api/v3/manualimport?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        IReadOnlyList<ManualImportFile>? response = await httpClient.GetFromJsonAsync<IReadOnlyList<ManualImportFile>>(path)
            .ConfigureAwait(false);

        return response ?? [];
    }
}