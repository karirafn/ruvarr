using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr.CachingSonarrClientTests;

public sealed class GetMissingEpisodesAsync
{
    private static MissingEpisode CreateMissingEpisode(int id = 1) =>
        new(
            Id: id,
            SeriesId: 10,
            TvdbId: 100,
            EpisodeFileId: 0,
            SeasonNumber: 1,
            Episodenumber: 1,
            AbsoluteEpisodeNumber: 1,
            Title: "Test Episode",
            Overview: "Overview",
            FinaleType: "",
            AirDate: new DateOnly(2025, 1, 1),
            AirDateUtc: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Runtime: 30,
            HasFile: false,
            Monitored: true,
            UnverifiedSceneNumbering: false,
            LastSearchTime: DateTime.UtcNow);

    [Fact]
    public async Task CacheMiss_CallsInnerClientAndCachesResult()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Act
        IReadOnlyCollection<MissingEpisode> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CacheHit_ReturnsCachedResult_WithoutCallingInnerClient()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Populate cache
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Act
        IReadOnlyCollection<MissingEpisode> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterManualImport_InvalidatesCache_AndFetchesFresh()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> firstResult = [CreateMissingEpisode(1)];
        IReadOnlyCollection<MissingEpisode> secondResult = [CreateMissingEpisode(2)];

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(firstResult, secondResult);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Populate cache
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Act
        await sut.ManualImportFilesAsync([], CancellationToken.None);
        IReadOnlyCollection<MissingEpisode> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(secondResult);
        await innerClient.Received(2).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSonarrUnreachable_WithWarmCache_ReturnsStaleCachedData()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Populate cache
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Make inner client throw
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Invalidate to force a fresh fetch attempt
        await sut.ManualImportFilesAsync([], CancellationToken.None);

        // Act
        IReadOnlyCollection<MissingEpisode> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task WhenCancelled_WithWarmCache_PropagatesCancellation()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> initial = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(initial);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Populate cache to set stale snapshot
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Make inner client throw OperationCanceledException
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        // Invalidate to force a fresh fetch attempt
        await sut.ManualImportFilesAsync([], CancellationToken.None);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task WhenCancelledWhileWaitingOnSemaphore_DoesNotOverReleaseSemaphore()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> expected = [CreateMissingEpisode()];
        TaskCompletionSource<IReadOnlyCollection<MissingEpisode>> tcs = new();
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Hold the semaphore by starting a call that blocks on the inner client
        Task<IReadOnlyCollection<MissingEpisode>> holdingCall = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // A second caller cancels while waiting on the semaphore
        using CancellationTokenSource cts = new();
        Task waitingCall = sut.GetMissingEpisodesAsync(cancellationToken: cts.Token);

        // Give the waiting call time to reach WaitAsync, then cancel
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => waitingCall);

        // Release the first call so the semaphore is freed normally
        tcs.SetResult(expected);
        await holdingCall;

        // Assert — if the semaphore was over-released, this second call would
        // enter concurrently with a future caller, but more directly: a subsequent
        // normal call should still succeed (semaphore count is exactly 1).
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([CreateMissingEpisode(2)]);
        cache.Remove("sonarr-missing-episodes");

        IReadOnlyCollection<MissingEpisode> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentColdCacheCalls_InnerClientCalledExactlyOnce()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        IReadOnlyCollection<MissingEpisode> expected = [CreateMissingEpisode()];
        TaskCompletionSource<IReadOnlyCollection<MissingEpisode>> tcs = new();

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        using CachingSonarrClient sut = new(
            () => innerClient, cache, NullLogger<CachingSonarrClient>.Instance);

        // Act
        Task<IReadOnlyCollection<MissingEpisode>> call1 = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);
        Task<IReadOnlyCollection<MissingEpisode>> call2 = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        tcs.SetResult(expected);

        IReadOnlyCollection<MissingEpisode>[] results = await Task.WhenAll(call1, call2);

        // Assert
        results[0].ShouldBe(expected);
        results[1].ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
