using Ruvarr.Blazor.Episodes;

namespace Ruvarr.Blazor;

internal sealed class RuvApiClient(HttpClient httpClient)
{
    public Task<List<EpisodeSummary>?> GetEpisodesAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<EpisodeSummary>>("/programs/episodes", cancellationToken);

    public async Task<bool> DownloadEpisodeAsync(string ruvId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsync(
            $"/programs/episodes/{ruvId}/download", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
