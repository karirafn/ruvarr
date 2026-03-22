using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class EpisodeMissingEventHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
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
    public async Task EnqueuesDownload_WhenEpisodeBecomesIsMissing()
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
        episode.Match(tvdbId: 1001, season: 1, episode: 1, isMissing: false);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        episode.UpdateMissingStatus(new HashSet<int> { 1001 });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert — the episode now has a queue item
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        RuvEpisode saved = await verifyContext.Set<RuvEpisode>()
            .Include(x => x.DownloadQueueItem)
            .Where(x => x.RuvId == "ABC123")
            .FirstAsync(cancellationToken);
        saved.DownloadQueueItem.ShouldNotBeNull();
    }

    [Fact]
    public async Task DoesNotEnqueueDuplicate_WhenEpisodeAlreadyQueued()
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
        episode.Match(tvdbId: 2001, season: 1, episode: 1, isMissing: false);
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act — episode already has a queue item; UpdateMissingStatus should not add another
        episode.UpdateMissingStatus(new HashSet<int> { 2001 });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert — still exactly one queue item for this episode
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvEpisode>()
            .Where(x => x.RuvId == "DEF456")
            .CountAsync(x => x.DownloadQueueItem != null, cancellationToken);
        count.ShouldBe(1);
    }
}
