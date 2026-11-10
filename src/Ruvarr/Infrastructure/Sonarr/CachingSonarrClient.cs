using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal sealed class CachingSonarrClient(
    Func<ISonarrClient> innerClientFactory,
    IMemoryCache cache) : ISonarrClient, IDisposable
{
    private const string CacheKey = "sonarr-missing-episodes";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Result<IReadOnlyCollection<MissingEpisode>>> GetMissingEpisodesAsync(
        int pageSize = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyCollection<MissingEpisode>? cached))
        {
            return new Result<IReadOnlyCollection<MissingEpisode>>(cached!);
        }

        bool acquired = false;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            acquired = true;

            if (cache.TryGetValue(CacheKey, out cached))
            {
                return new Result<IReadOnlyCollection<MissingEpisode>>(cached!);
            }

            ISonarrClient innerClient = innerClientFactory();
            Result<IReadOnlyCollection<MissingEpisode>> result =
                await innerClient.GetMissingEpisodesAsync(pageSize, cancellationToken);

            // A failure is never cached: "Sonarr is down" must not be served later as "Sonarr has nothing".
            if (result.IsSuccess)
            {
                cache.Set(CacheKey, result.FromResult(), new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration,
                    Size = 1
                });
            }

            return result;
        }
        finally
        {
            if (acquired) _semaphore.Release();
        }
    }

    public async Task ManualImportFilesAsync(
        IEnumerable<ManualImportRequest> files,
        CancellationToken cancellationToken = default)
    {
        ISonarrClient innerClient = innerClientFactory();
        await innerClient.ManualImportFilesAsync(files, cancellationToken);
        cache.Remove(CacheKey);
    }

    public Task<Result<IReadOnlyList<Series>>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetSeriesAsync(cancellationToken);

    public Task<Result<IReadOnlyList<SonarrEpisode>>> GetEpisodesAsync(int seriesId, CancellationToken cancellationToken = default) =>
        innerClientFactory().GetEpisodesAsync(seriesId, cancellationToken);

    public Task<Result<IReadOnlyList<ManualImportFile>>> GetManualImportsAsync(
        string folder,
        int? seriesId = null,
        CancellationToken cancellationToken = default) =>
        innerClientFactory().GetManualImportsAsync(folder, seriesId, cancellationToken);

    public Task<Result<IReadOnlyList<RootFolder>>> GetRootFoldersAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetRootFoldersAsync(cancellationToken);

    public Task<Result<IReadOnlyList<QualityProfile>>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetQualityProfilesAsync(cancellationToken);

    public Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default) =>
        innerClientFactory().AddSeriesAsync(request, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
