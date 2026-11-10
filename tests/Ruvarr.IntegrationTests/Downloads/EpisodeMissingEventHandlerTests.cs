using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Contracts;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

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
    public async Task EnqueuesDownload_WhenEpisodeBecomesMissing()
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
        episode.Match(tvdbId: 1001, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Automatic);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        episode.UpdateSonarrStatus(new Dictionary<int, SonarrEpisodeStatus> { [1001] = SonarrEpisodeStatus.Missing });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert — the episode now has a queue item
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        RuvEpisode saved = await verifyContext.Set<RuvEpisode>()
            .Include(x => x.DownloadQueueItems)
            .Where(x => x.RuvId == "ABC123")
            .FirstAsync(cancellationToken);
        saved.DownloadQueueItems.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DoesNotEnqueueDuplicate_WhenActiveItemExists()
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
        episode.Match(tvdbId: 2001, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Automatic);
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act — episode already has a Pending queue item; UpdateSonarrStatus should not add another
        episode.UpdateSonarrStatus(new Dictionary<int, SonarrEpisodeStatus> { [2001] = SonarrEpisodeStatus.Missing });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert — still exactly one queue item
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task EnqueuesNewItem_WhenOnlyCompletedItemExists()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(3, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("GHI789", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        episode.Match(tvdbId: 3001, season: 1, episode: 1, sonarrStatuses: SonarrStatuses.NotTracked, origin: MatchOrigin.Automatic);
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"UPDATE download_queue SET status = 'Complete', downloaded = {DateTime.UtcNow.ToString("o")} WHERE episode_id = (SELECT id FROM episodes WHERE ruv_id = 'GHI789')",
            cancellationToken);

        // Act — only completed items exist; UpdateSonarrStatus should add a new one
        episode.UpdateSonarrStatus(new Dictionary<int, SonarrEpisodeStatus> { [3001] = SonarrEpisodeStatus.Missing });
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert — two queue items total (one complete, one pending)
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(2);
    }
}