using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Commands.DeleteDownloadQueueItem;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads.Commands.DeleteDownloadQueueItem;

public sealed class DeleteDownloadQueueItemHandlerTests(IntegrationTestFactory factory)
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
    public async Task DeletesPendingItem()
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
        arrangeContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new DeleteDownloadQueueItemCommand("EP001"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task DeletesFailedItem()
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
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkFailed();
        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new DeleteDownloadQueueItemCommand("EP002"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsNotDeletable_WhenItemIsDownloading()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP003", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkDownloading();
        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new DeleteDownloadQueueItemCommand("EP003"), cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotDeletableCode);
    }

    [Fact]
    public async Task DeletesCompleteItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP004", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        item.MarkDownloading();
        item.MarkDownloaded();
        arrangeContext.Set<DownloadQueueItem>().Add(item);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new DeleteDownloadQueueItemCommand("EP004"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task ReturnsNotFound_WhenItemDoesNotExist()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        RuvarrResult result = await handler.Handle(new DeleteDownloadQueueItemCommand("NONEXISTENT"), cancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(DownloadErrors.ItemNotFoundCode);
    }
}
