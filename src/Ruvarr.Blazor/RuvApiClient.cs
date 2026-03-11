using Ruvarr.Blazor.Programs;

namespace Ruvarr.Blazor;

internal sealed class RuvApiClient(HttpClient httpClient)
{
    public Task<List<ProgramSummary>?> GetEpisodesAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes", cancellationToken);

    public async Task<bool> DownloadEpisodeAsync(string ruvId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsync(
            $"/programs/episodes/{ruvId}/download", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
