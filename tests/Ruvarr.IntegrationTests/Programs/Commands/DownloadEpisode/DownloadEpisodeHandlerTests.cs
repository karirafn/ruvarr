using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Commands.DownloadEpisode;

public sealed class DownloadEpisodeHandlerTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private const string EpisodeRuvId = "EP001";

    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync();

        program.TryAddEpisode(EpisodeRuvId, new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task WhenConcurrentWriterQueuesSameEpisode_ReturnsSuccess_AndLeavesSingleUnfinishedItem()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddTransient<IDomainEventHandler<EpisodeDownloadRequestedEvent>, ConcurrentQueueWriter>()));

        // Act
        RuvarrResult result = await DownloadAsync(customFactory, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        List<DownloadQueueStatus> statuses = await verifyContext.Set<DownloadQueueItem>()
            .Select(x => x.Status)
            .ToListAsync(cancellationToken);
        statuses.ShouldBe([DownloadQueueStatus.Pending]);
    }

    [Fact]
    public async Task WhenSaveFailsForAnotherReason_Throws()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using WebApplicationFactory<Program> customFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddTransient<IDomainEventHandler<EpisodeDownloadRequestedEvent>, ConcurrentEpisodeDeleter>()));

        // Act & Assert
        await Should.ThrowAsync<DbUpdateException>(() => DownloadAsync(customFactory, cancellationToken));
    }

    private static async Task<RuvarrResult> DownloadAsync(WebApplicationFactory<Program> customFactory, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = customFactory.Services.CreateAsyncScope();
        IRequestHandler<DownloadEpisodeCommand> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<DownloadEpisodeCommand>>();
        return await handler.Handle(new DownloadEpisodeCommand(EpisodeRuvId), cancellationToken);
    }

    // Runs after the production handler has staged its item, so the competing row is committed
    // first and the production handler's insert hits the unfinished-item unique index.
    private sealed class ConcurrentQueueWriter(IServiceScopeFactory scopeFactory) : IDomainEventHandler<EpisodeDownloadRequestedEvent>
    {
        public async Task Handle(EpisodeDownloadRequestedEvent @event, CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
            RuvEpisode episode = await dbContext.Set<RuvEpisode>()
                .Where(x => x.RuvId == @event.Episode.RuvId)
                .FirstAsync(cancellationToken);
            dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // Removes the episode so the production handler's insert hits a foreign-key violation instead.
    private sealed class ConcurrentEpisodeDeleter(IServiceScopeFactory scopeFactory) : IDomainEventHandler<EpisodeDownloadRequestedEvent>
    {
        public async Task Handle(EpisodeDownloadRequestedEvent @event, CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
            await dbContext.Set<RuvEpisode>()
                .Where(x => x.RuvId == @event.Episode.RuvId)
                .ExecuteDeleteAsync(cancellationToken);
        }
    }
}
