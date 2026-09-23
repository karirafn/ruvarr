using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Migrations;

public sealed class AddEpisodeCreatedBackfillTests : IAsyncLifetime
{
    private const string TargetMigration = "20260904171623_AddDownloadQueueItemRetryFields";
    private const string AddEpisodeCreatedMigration = "20260923083649_AddEpisodeCreated";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task BackfillSetsCreatedEqualToFirstRun_ForPreExistingEpisodeRows()
    {
        // Arrange — apply all migrations up to the one before AddEpisodeCreated
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        SqliteConnectionStringBuilder connectionString = new()
        {
            DataSource = _dbPath,
            Pooling = false,
        };

        DbContextOptions<RuvarrDbContext> options = new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseSqlite(connectionString.ToString())
            .UseSnakeCaseNamingConvention()
            .Options;

        await using (RuvarrDbContext preContext = new(options, null!))
        {
            await preContext.Database.MigrateAsync(TargetMigration, cancellationToken);

            // Insert a program row (required as FK for the episode); use a fixed id via INSERT OR REPLACE
            await preContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO programs (id, ruv_id, channel, name, has_multiple_episodes, created, has_missing_episodes, is_monitored, lookup_count) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})",
                [1, 99901, "RÚV1", "Backfill Test Program", 0, "2026-01-01T00:00:00.0000000+00:00", 0, 0, 0],
                cancellationToken);

            // Insert an episode with a known first_run; created column does not yet exist
            DateTime knownFirstRun = new(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
            string firstRunText = knownFirstRun.ToString("O");

            await preContext.Database.ExecuteSqlRawAsync(
                "INSERT INTO episodes (ruv_id, uri, title, description, first_run, duration_seconds, lookup_count, program_id) VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})",
                ["BF001", "https://example.com/ep.mp4", "Backfill Episode", "Desc", firstRunText, 1800, 0, 1],
                cancellationToken);
        }

        // Act — apply the AddEpisodeCreated migration, which adds the column and runs the backfill
        await using (RuvarrDbContext migrateContext = new(options, null!))
        {
            await migrateContext.Database.MigrateAsync(AddEpisodeCreatedMigration, cancellationToken);
        }

        // Assert — the created column is now set to first_run
        await using (RuvarrDbContext assertContext = new(options, null!))
        {
            DateTime created = await assertContext.Database
                .SqlQuery<DateTime>($"SELECT created AS Value FROM episodes WHERE ruv_id = 'BF001'")
                .FirstAsync(cancellationToken);

            DateTime firstRun = await assertContext.Database
                .SqlQuery<DateTime>($"SELECT first_run AS Value FROM episodes WHERE ruv_id = 'BF001'")
                .FirstAsync(cancellationToken);

            created.ShouldBe(firstRun);
        }
    }
}
