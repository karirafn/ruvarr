using System.Collections.Specialized;
using System.Web;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal sealed class SonarrClient(ILogger<SonarrClient> logger, HttpClient httpClient)
    : ApiClient(logger, httpClient), ISonarrClient
{
    public Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        GetMany<Series>("api/v3/series", cancellationToken);

    public async Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("pageSize", $"{pageSize}");

        string path = $"api/v3/wanted/missing?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        MissingEpisodesResponse? response = await GetAsync<MissingEpisodesResponse>(path, cancellationToken);

        return response?.Records ?? [];
    }

    public Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default) =>
        PostAsync("api/v3/command", new ManualImportCommand(files), cancellationToken);

    public Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("folder", folder);

        string path = $"api/v3/manualimport?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        return GetMany<ManualImportFile>(path, cancellationToken: cancellationToken);
    }
}