using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class EpisodeDownloadRequestedEventHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task CreatesDownloadQueueItem_WhenDownloadRequested()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("ABC123", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];

        // Act
        episode.RequestDownload();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        RuvEpisode saved = await verifyContext.Set<RuvEpisode>()
            .Include(x => x.DownloadQueueItems)
            .Where(x => x.RuvId == "ABC123")
            .FirstAsync(cancellationToken);
        saved.DownloadQueueItems.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CreatesSecondQueueItem_WhenEpisodeAlreadyQueued()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(2, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("DEF456", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        episode.RequestDownload();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(2);
    }
}
