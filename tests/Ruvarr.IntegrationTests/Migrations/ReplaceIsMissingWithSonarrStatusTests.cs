using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Ruvarr.IntegrationTests.Migrations;

public sealed class ReplaceIsMissingWithSonarrStatusTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261110081653_AddDownloadQueueUnfinishedItemIndex";
    private const string Migration = "20261110133404_ReplaceIsMissingWithSonarrStatus";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ServiceProvider _services = new ServiceCollection().BuildServiceProvider();
    private RuvarrDbContext _dbContext = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        DbContextOptions<RuvarrDbContext> options = new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseSqlite(_connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        _dbContext = new RuvarrDbContext(options, _services);
        await MigrateToAsync(PreviousMigration);
        await DisableForeignKeysAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private Task MigrateToAsync(string migration) =>
        _dbContext.GetService<IMigrator>().MigrateAsync(migration, TestContext.Current.CancellationToken);

    private async Task DisableForeignKeysAsync()
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task InsertLinkWithIsMissingAsync(int tvdbId, bool isMissing)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO tvdb_episodes (episode_id, tvdb_id, season_number, episode_number, is_missing, has_isl_translation) " +
            "VALUES (1, $tvdbId, 1, 1, $isMissing, 0);";
        command.Parameters.AddWithValue("$tvdbId", tvdbId);
        command.Parameters.AddWithValue("$isMissing", isMissing);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task InsertLinkWithStatusAsync(int tvdbId, string status)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO tvdb_episodes (episode_id, tvdb_id, season_number, episode_number, sonarr_status, has_isl_translation) " +
            "VALUES (1, $tvdbId, 1, 1, $status, 0);";
        command.Parameters.AddWithValue("$tvdbId", tvdbId);
        command.Parameters.AddWithValue("$status", status);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<object?> StoredSonarrStatusAsync(int tvdbId)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT sonarr_status FROM tvdb_episodes WHERE tvdb_id = $tvdbId;";
        command.Parameters.AddWithValue("$tvdbId", tvdbId);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private async Task<object?> StoredIsMissingAsync(int tvdbId)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT is_missing FROM tvdb_episodes WHERE tvdb_id = $tvdbId;";
        command.Parameters.AddWithValue("$tvdbId", tvdbId);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Up_MapsIsMissingTrueToMissing()
    {
        await InsertLinkWithIsMissingAsync(tvdbId: 1, isMissing: true);

        await MigrateToAsync(Migration);

        (await StoredSonarrStatusAsync(tvdbId: 1)).ShouldBe("Missing");
    }

    [Fact]
    public async Task Up_MapsIsMissingFalseToNotTracked()
    {
        await InsertLinkWithIsMissingAsync(tvdbId: 1, isMissing: false);

        await MigrateToAsync(Migration);

        (await StoredSonarrStatusAsync(tvdbId: 1)).ShouldBe("NotTracked");
    }

    [Theory]
    [InlineData("Missing", 1L)]
    [InlineData("Downloaded", 0L)]
    [InlineData("NotTracked", 0L)]
    public async Task Down_MapsOnlyMissingToIsMissingTrue(string status, long expectedIsMissing)
    {
        await MigrateToAsync(Migration);
        await DisableForeignKeysAsync();
        await InsertLinkWithStatusAsync(tvdbId: 1, status);

        await MigrateToAsync(PreviousMigration);

        (await StoredIsMissingAsync(tvdbId: 1)).ShouldBe(expectedIsMissing);
    }
}
