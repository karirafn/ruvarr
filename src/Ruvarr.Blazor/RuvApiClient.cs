using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Ruvarr.Blazor.DownloadQueue;
using Ruvarr.Blazor.Programs;

namespace Ruvarr.Blazor;

internal sealed class RuvApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<List<ProgramSummary>?> GetEpisodesAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes", cancellationToken);

    public Task<List<DownloadQueueItemSummary>?> GetDownloadQueueAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<DownloadQueueItemSummary>>("/programs/download-queue", cancellationToken);

    public async IAsyncEnumerable<List<DownloadQueueItemSummary>> WatchDownloadQueueAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            "/programs/download-queue/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        SseParser<List<DownloadQueueItemSummary>> parser = SseParser.Create(
            stream,
            (_, data) => JsonSerializer.Deserialize<List<DownloadQueueItemSummary>>(data, JsonOptions)!);

        await foreach (SseItem<List<DownloadQueueItemSummary>> item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }

    public async Task<bool> DownloadEpisodeAsync(string ruvId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsync(
            $"/programs/episodes/{ruvId}/download", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> MatchEpisodeAsync(string ruvId, int tvdbId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsync(
            $"/programs/episodes/{ruvId}/match/{tvdbId}", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
