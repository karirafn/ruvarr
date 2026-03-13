using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;

using Ruvarr.Contracts;

namespace Ruvarr.Blazor;

internal sealed class RuvApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<List<ProgramSummary>?> GetEpisodesAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<ProgramSummary>>("/programs/episodes", cancellationToken);

    public Task<List<ProgramSummary>?> GetUnmatchedEpisodesAsync(CancellationToken cancellationToken = default)
        => httpClient.GetFromJsonAsync<List<ProgramSummary>>(
            "/programs/episodes?isProgramMissingEpisodes=true&isEpisodeMatched=false",
            cancellationToken);

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

    public async Task<bool> RefreshProgramAsync(int ruvId, CancellationToken cancellationToken = default)
    {
        HttpResponseMessage response = await httpClient.PostAsync(
            $"/programs/{ruvId}/refresh", content: null, cancellationToken);
        return response.IsSuccessStatusCode;
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

    public async IAsyncEnumerable<List<TvdbEpisodeLookupQueueItemSummary>> WatchTvdbEpisodeLookupQueueAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            "/programs/tvdb-episode-lookup-queue/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        SseParser<List<TvdbEpisodeLookupQueueItemSummary>> parser = SseParser.Create(
            stream,
            (_, data) => JsonSerializer.Deserialize<List<TvdbEpisodeLookupQueueItemSummary>>(data, JsonOptions)!);

        await foreach (SseItem<List<TvdbEpisodeLookupQueueItemSummary>> item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }

    public async IAsyncEnumerable<List<TvdbSeriesLookupQueueItemSummary>> WatchTvdbSeriesLookupQueueAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            "/programs/tvdb-series-lookup-queue/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        SseParser<List<TvdbSeriesLookupQueueItemSummary>> parser = SseParser.Create(
            stream,
            (_, data) => JsonSerializer.Deserialize<List<TvdbSeriesLookupQueueItemSummary>>(data, JsonOptions)!);

        await foreach (SseItem<List<TvdbSeriesLookupQueueItemSummary>> item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }

    public async IAsyncEnumerable<List<ProgramRefreshQueueItemSummary>> WatchProgramRefreshQueueAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            "/programs/program-refresh-queue/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        SseParser<List<ProgramRefreshQueueItemSummary>> parser = SseParser.Create(
            stream,
            (_, data) => JsonSerializer.Deserialize<List<ProgramRefreshQueueItemSummary>>(data, JsonOptions)!);

        await foreach (SseItem<List<ProgramRefreshQueueItemSummary>> item in parser.EnumerateAsync(cancellationToken))
        {
            yield return item.Data;
        }
    }
}
