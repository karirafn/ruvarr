using Microsoft.Extensions.Caching.Memory;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Ruvarr.Abstractions;
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

    private static Result<IReadOnlyCollection<MissingEpisode>> Success(params MissingEpisode[] episodes) =>
        new(episodes);

    private static Result<IReadOnlyCollection<MissingEpisode>> Failure() =>
        new(ApiClientErrors.RequestFailed);

    [Fact]
    public async Task CacheMiss_CallsInnerClientAndCachesResult()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        MissingEpisode[] expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Success(expected));

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Act
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.FromResult().ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CacheHit_ReturnsCachedResult_WithoutCallingInnerClient()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        MissingEpisode[] expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Success(expected));

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Populate cache
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Act
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.FromResult().ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AfterManualImport_InvalidatesCache_AndFetchesFresh()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        MissingEpisode[] secondResult = [CreateMissingEpisode(2)];

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Success(CreateMissingEpisode(1)), Success(secondResult));

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Populate cache
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Act
        await sut.ManualImportFilesAsync([], CancellationToken.None);
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.FromResult().ShouldBe(secondResult);
        await innerClient.Received(2).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSonarrFails_ReturnsFailure()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Failure());

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Act
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ApiClientErrors.RequestFailed);
    }

    [Fact]
    public async Task WhenSonarrFails_CachesNothing()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        MissingEpisode[] expected = [CreateMissingEpisode()];
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Failure(), Success(expected));

        using CachingSonarrClient sut = new(() => innerClient, cache);

        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Act
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.FromResult().ShouldBe(expected);
        await innerClient.Received(2).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WhenSonarrFails_WithPreviouslyFetchedData_ReturnsFailureNotStaleData()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Success(CreateMissingEpisode()), Failure());

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Populate cache, then invalidate to force a fresh fetch attempt
        await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);
        await sut.ManualImportFilesAsync([], CancellationToken.None);

        // Act
        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task WhenCancelled_PropagatesCancellation()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        using CachingSonarrClient sut = new(() => innerClient, cache);

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

        TaskCompletionSource<Result<IReadOnlyCollection<MissingEpisode>>> tcs = new();
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Hold the semaphore by starting a call that blocks on the inner client
        Task<Result<IReadOnlyCollection<MissingEpisode>>> holdingCall = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        // A second caller cancels while waiting on the semaphore
        using CancellationTokenSource cts = new();
        Task waitingCall = sut.GetMissingEpisodesAsync(cancellationToken: cts.Token);

        // Give the waiting call time to reach WaitAsync, then cancel
        await Task.Delay(50, TestContext.Current.CancellationToken);
        await cts.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(() => waitingCall);

        // Release the first call so the semaphore is freed normally
        tcs.SetResult(Success(CreateMissingEpisode()));
        await holdingCall;

        // Assert — a subsequent normal call should still succeed (semaphore count is exactly 1).
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Success(CreateMissingEpisode(2)));
        cache.Remove("sonarr-missing-episodes");

        Result<IReadOnlyCollection<MissingEpisode>> result = await sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);
        result.FromResult().Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentColdCacheCalls_InnerClientCalledExactlyOnce()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        using MemoryCache cache = new(new MemoryCacheOptions());

        MissingEpisode[] expected = [CreateMissingEpisode()];
        TaskCompletionSource<Result<IReadOnlyCollection<MissingEpisode>>> tcs = new();

        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        using CachingSonarrClient sut = new(() => innerClient, cache);

        // Act
        Task<Result<IReadOnlyCollection<MissingEpisode>>> call1 = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);
        Task<Result<IReadOnlyCollection<MissingEpisode>>> call2 = sut.GetMissingEpisodesAsync(cancellationToken: CancellationToken.None);

        tcs.SetResult(Success(expected));

        Result<IReadOnlyCollection<MissingEpisode>>[] results = await Task.WhenAll(call1, call2);

        // Assert
        results[0].FromResult().ShouldBe(expected);
        results[1].FromResult().ShouldBe(expected);
        await innerClient.Received(1).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
