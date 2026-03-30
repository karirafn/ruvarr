using Microsoft.Extensions.Caching.Memory;

using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Infrastructure.Sonarr;

internal sealed class CachingSonarrClient(
    Func<ISonarrClient> innerClientFactory,
    IMemoryCache cache,
    ILogger<CachingSonarrClient> logger) : ISonarrClient, IDisposable
{
    private const string CacheKey = "sonarr-missing-episodes";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IReadOnlyCollection<MissingEpisode>? _staleSnapshot;

    public async Task<IReadOnlyCollection<MissingEpisode>> GetMissingEpisodesAsync(
        int pageSize = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyCollection<MissingEpisode>? cached))
        {
            return cached!;
        }

        bool acquired = false;
        try
        {
            await _semaphore.WaitAsync(cancellationToken);
            acquired = true;

            if (cache.TryGetValue(CacheKey, out cached))
            {
                return cached!;
            }

            try
            {
                ISonarrClient innerClient = innerClientFactory();
                IReadOnlyCollection<MissingEpisode> result =
                    await innerClient.GetMissingEpisodesAsync(pageSize, cancellationToken);

                _staleSnapshot = result;

                cache.Set(CacheKey, result, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = CacheDuration,
                    Size = 1
                });

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException && _staleSnapshot is not null)
            {
                logger.LogWarning(ex, "Sonarr unreachable, returning stale cached missing episodes");
                return _staleSnapshot;
            }
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

    public Task<IReadOnlyList<Series>> GetSeriesAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetSeriesAsync(cancellationToken);

    public Task<IReadOnlyList<ManualImportFile>> GetManualImportsAsync(
        string folder,
        int? seriesId = null,
        CancellationToken cancellationToken = default) =>
        innerClientFactory().GetManualImportsAsync(folder, seriesId, cancellationToken);

    public Task<IReadOnlyList<RootFolder>> GetRootFoldersAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetRootFoldersAsync(cancellationToken);

    public Task<IReadOnlyList<QualityProfile>> GetQualityProfilesAsync(CancellationToken cancellationToken = default) =>
        innerClientFactory().GetQualityProfilesAsync(cancellationToken);

    public Task<Series?> AddSeriesAsync(AddSeriesRequest request, CancellationToken cancellationToken = default) =>
        innerClientFactory().AddSeriesAsync(request, cancellationToken);

    public void Dispose() => _semaphore.Dispose();
}
