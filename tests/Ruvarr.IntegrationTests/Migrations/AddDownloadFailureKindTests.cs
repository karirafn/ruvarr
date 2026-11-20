using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Ruvarr.IntegrationTests.Migrations;

public sealed class AddDownloadFailureKindTests : IAsyncLifetime
{
    private const string PreviousMigration = "20261110163028_AddProgramManualSeasons";
    private const string Migration = "20261120124605_AddDownloadFailureKind";

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

    private async Task InsertFailedQueueItemAsync(int id)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText =
            "INSERT INTO download_queue (id, episode_id, created, status, retry_count, failure_reason) " +
            "VALUES ($id, 1, '2026-01-01', 'Failed', 1, 'Sonarr scan did not include the file');";
        command.Parameters.AddWithValue("$id", id);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<object?> StoredFailureKindAsync(int id)
    {
        await using SqliteCommand command = _connection.CreateCommand();
        command.CommandText = "SELECT failure_kind FROM download_queue WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Up_SetsExistingRowsToUnclassified()
    {
        await InsertFailedQueueItemAsync(1);

        await _dbContext.GetService<IMigrator>().MigrateAsync(Migration, TestContext.Current.CancellationToken);

        (await StoredFailureKindAsync(1)).ShouldBe("Unclassified");
    }
}
