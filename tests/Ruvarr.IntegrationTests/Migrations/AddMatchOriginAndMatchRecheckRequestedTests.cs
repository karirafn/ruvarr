using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Ruvarr.IntegrationTests.Migrations;

public sealed class AddMatchOriginAndMatchRecheckRequestedTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261110133404_ReplaceIsMissingWithSonarrStatus";
    private const string Migration = "20261110155825_AddMatchOriginAndMatchRecheckRequested";

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
        await _dbContext.GetService<IMigrator>().MigrateAsync(PreviousMigration, TestContext.Current.CancellationToken);
        await DisableForeignKeysAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task DisableForeignKeysAsync()
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = OFF;";
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task InsertEpisodeAsync(int id)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO episodes (id, ruv_id, uri, title, description, first_run, created, lookup_count, duration_seconds, program_id) " +
            "VALUES ($id, $ruvId, 'http://test.com', 'Title', '', '2026-01-01', '2026-01-01', 0, 0, 1);";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$ruvId", $"ep{id:D4}");
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task InsertLinkAsync(int episodeId, int tvdbId)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO tvdb_episodes (episode_id, tvdb_id, season_number, episode_number, sonarr_status, has_isl_translation) " +
            "VALUES ($episodeId, $tvdbId, 1, 1, 'NotTracked', 0);";
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.Parameters.AddWithValue("$tvdbId", tvdbId);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<object?> StoredMatchOriginAsync(int episodeId)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT match_origin FROM episodes WHERE id = $episodeId;";
        command.Parameters.AddWithValue("$episodeId", episodeId);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    private Task MigrateAsync() =>
        _dbContext.GetService<IMigrator>().MigrateAsync(Migration, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Up_BackfillsAMultiLinkEpisodeAsManual()
    {
        await InsertEpisodeAsync(1);
        await InsertLinkAsync(episodeId: 1, tvdbId: 101);
        await InsertLinkAsync(episodeId: 1, tvdbId: 102);

        await MigrateAsync();

        (await StoredMatchOriginAsync(1)).ShouldBe("Manual");
    }

    [Fact]
    public async Task Up_BackfillsASingleLinkEpisodeAsAutomatic()
    {
        await InsertEpisodeAsync(1);
        await InsertLinkAsync(episodeId: 1, tvdbId: 101);

        await MigrateAsync();

        (await StoredMatchOriginAsync(1)).ShouldBe("Automatic");
    }

    [Fact]
    public async Task Up_LeavesAnUnmatchedEpisodeWithoutOrigin()
    {
        await InsertEpisodeAsync(1);

        await MigrateAsync();

        (await StoredMatchOriginAsync(1)).ShouldBe(DBNull.Value);
    }
}
