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

    public Task<Result<IReadOnlyList<SonarrEpisode>>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default) =>
        GetMany<SonarrEpisode>($"api/v3/episode?seriesId={seriesId}", cancellationToken);

    public async Task<Result<IReadOnlyCollection<MissingEpisode>>> GetMissingEpisodesAsync(int pageSize = int.MaxValue, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("pageSize", $"{pageSize}");

        string path = $"api/v3/wanted/missing?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        Result<MissingEpisodesResponse> result = await GetAsync<MissingEpisodesResponse>(path, cancellationToken);

        return result.Match(
            v => new Result<IReadOnlyCollection<MissingEpisode>>(v.Records),
            error => new Result<IReadOnlyCollection<MissingEpisode>>(error));
    }

    public Task ManualImportFilesAsync(IEnumerable<ManualImportRequest> files, CancellationToken cancellationToken = default) =>
        PostAsync("api/v3/command", new ManualImportCommand(files), cancellationToken);

    public Task<Result<IReadOnlyList<ManualImportFile>>> GetManualImportsAsync(string folder, int? seriesId = null, CancellationToken cancellationToken = default)
    {
        NameValueCollection parameters = HttpUtility.ParseQueryString(string.Empty);
        parameters.Add("folder", folder);
        if (seriesId.HasValue)
        {
            parameters.Add("seriesId", $"{seriesId.Value}");
        }

        string path = $"api/v3/manualimport?{HttpUtility.UrlPathEncode(parameters.ToString())}";

        return GetMany<ManualImportFile>(path, cancellationToken);
    }

    public Task<Result<IReadOnlyList<RootFolder>>> GetRootFoldersAsync(CancellationToken cancellationToken = default) =>
        GetMany<RootFolder>("api/v3/rootfolder", cancellationToken);

    public Task<Result<IReadOnlyList<QualityProfile>>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        GetMany<QualityProfile>("api/v3/qualityprofile", cancellationToken);

    public Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<AddSeriesRequest, Series>("api/v3/series", request, cancellationToken);

    public Task<Result<IReadOnlyList<SonarrNotification>>> GetNotificationsAsync(CancellationToken cancellationToken = default) =>
        GetMany<SonarrNotification>("api/v3/notification", cancellationToken);

    public Task<RuvarrResult> TestNotificationAsync(SonarrNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/v3/notification/test", notification, cancellationToken);

    public Task<RuvarrResult> CreateNotificationAsync(SonarrNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, "api/v3/notification", notification, cancellationToken);

    public Task<RuvarrResult> UpdateNotificationAsync(int id, SonarrNotification notification, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Put, $"api/v3/notification/{id}", notification with { Id = id }, cancellationToken);
}
