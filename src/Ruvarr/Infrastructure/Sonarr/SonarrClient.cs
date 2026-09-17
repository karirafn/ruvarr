using System.Collections.Specialized;
using System.Web;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal sealed class SonarrClient(ILogger<SonarrClient> logger, HttpClient httpClient)
    : ApiClient(logger, httpClient), ISonarrClient
{
    public Task<Result<IReadOnlyList<Series>>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        GetMany<Series>("api/v3/series", cancellationToken);

    public async Task<IReadOnlyList<SonarrEpisode>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<SonarrEpisode>> result = await GetMany<SonarrEpisode>($"api/v3/episode?seriesId={seriesId}", cancellationToken);
        return result.Match(v => v, _ => []);
    }

    public async Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("pageSize", $"{pageSize}");

        string path = $"api/v3/wanted/missing?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        Result<MissingEpisodesResponse> result = await GetAsync<MissingEpisodesResponse>(path, cancellationToken);

        return result.Match(v => (IReadOnlyCollection<MissingEpisode>)v.Records, _ => []);
    }

    public Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default) =>
        PostAsync("api/v3/command", new ManualImportCommand(files), cancellationToken);

    public async Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(string folder, int? seriesId = null, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("folder", folder);
        if (seriesId.HasValue)
        {
            parameters.Add("seriesId", $"{seriesId.Value}");
        }

        string path = $"api/v3/manualimport?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        Result<IReadOnlyList<ManualImportFile>> result = await GetMany<ManualImportFile>(path, cancellationToken);
        return result.Match(v => v, _ => []);
    }

    public async Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<RootFolder>> result = await GetMany<RootFolder>("api/v3/rootfolder", cancellationToken);
        return result.Match(v => v, _ => []);
    }

    public async Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<QualityProfile>> result = await GetMany<QualityProfile>("api/v3/qualityprofile", cancellationToken);
        return result.Match(v => v, _ => []);
    }

    public Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AddSeriesRequest, Series>("api/v3/series", request, cancellationToken);
}
