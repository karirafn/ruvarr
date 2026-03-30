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

    public Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder, int? seriesId = null, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("folder", folder);
        if (seriesId.HasValue)
            parameters.Add("seriesId", $"{seriesId.Value}");

        string path = $"api/v3/manualimport?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        return GetMany<ManualImportFile>(path, cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default) =>
        GetMany<RootFolder>("api/v3/rootfolder", cancellationToken);

    public Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        GetMany<QualityProfile>("api/v3/qualityprofile", cancellationToken);

    public Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AddSeriesRequest, Series>("api/v3/series", request, cancellationToken);
}