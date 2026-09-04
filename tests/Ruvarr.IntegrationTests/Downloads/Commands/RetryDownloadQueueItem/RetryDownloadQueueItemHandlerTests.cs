using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Commands.RetryDownloadQueueItem;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads.Commands.RetryDownloadQueueItem;

public sealed class RetryDownloadQueueItemHandlerTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task WhenItemIsFailed_ReturnsSuccess_AndStatusIsPending()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP001", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .WithEpisode(episode)
            .Failed("network error")
            .Build();

        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<RetryDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<RetryDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new RetryDownloadQueueItemCommand("EP001"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        DownloadQueueItem? saved = await verifyContext.Set<DownloadQueueItem>()
            .Where(x => x.Episode.RuvId == "EP001")
            .FirstOrDefaultAsync(cancellationToken);

        saved.ShouldNotBeNull();
        saved.ShouldSatisfyAllConditions(
            () => saved.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => saved.NextRetryAt.ShouldBeNull(),
            () => saved.FailureReason.ShouldBeNull(),
            () => saved.RetryCount.ShouldBe(1));
    }

    [Fact]
    public async Task WhenItemIsExhausted_ReturnsSuccess_AndStatusIsPending()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP002", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = new DownloadQueueItemBuilder()
            .WithEpisode(episode)
            .Exhausted()
            .Build();

        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<RetryDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<RetryDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new RetryDownloadQueueItemCommand("EP002"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        DownloadQueueItem? saved = await verifyContext.Set<DownloadQueueItem>()
            .Where(x => x.Episode.RuvId == "EP002")
            .FirstOrDefaultAsync(cancellationToken);

        saved.ShouldNotBeNull();
        saved.ShouldSatisfyAllConditions(
            () => saved.Status.ShouldBe(DownloadQueueStatus.Pending),
            () => saved.NextRetryAt.ShouldBeNull(),
            () => saved.FailureReason.ShouldBeNull());
    }

    [Theory]
    [InlineData(DownloadQueueStatus.Downloading)]
    [InlineData(DownloadQueueStatus.Pending)]
    [InlineData(DownloadQueueStatus.Complete)]
    public async Task WhenItemIsNotRetryable_ReturnsItemNotRetryable(DownloadQueueStatus status)
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        string episodeRuvId = $"EP-{status}";
        program.TryAddEpisode(episodeRuvId, new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];

        DownloadQueueItem item = status switch
        {
            DownloadQueueStatus.Downloading => BuildDownloading(episode),
            DownloadQueueStatus.Pending => DownloadQueueItem.Create(episode),
            DownloadQueueStatus.Complete => new DownloadQueueItemBuilder().WithEpisode(episode).Downloaded().Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<RetryDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<RetryDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new RetryDownloadQueueItemCommand(episodeRuvId), cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotRetryableCode);
    }

    [Fact]
    public async Task WhenItemDoesNotExist_ReturnsItemNotFound()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<RetryDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<RetryDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new RetryDownloadQueueItemCommand("NONEXISTENT"), cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotFoundCode);
    }

    private static DownloadQueueItem BuildDownloading(RuvEpisode episode)
    {
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkDownloading();
        return item;
    }
}
