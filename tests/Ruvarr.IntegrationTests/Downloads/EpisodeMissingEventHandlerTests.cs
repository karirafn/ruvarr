using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Downloads.Extensions;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class EpisodeMissingEventHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
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

        program.TryAddEpisode("ABC123", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];

        // Act
        episode.SetMissing(true);
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

        program.TryAddEpisode("DEF456", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.EnqueueDownload(episode);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act — episode already has a queue item; SetMissing(true) should not add another
        episode.SetMissing(true);
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
