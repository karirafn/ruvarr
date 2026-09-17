using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.SonarrImporterTests;

public sealed class GetSeriesFailure
{
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly RuvarrSettings _settings;
    private readonly string _tempDownloadsRoot;

    public GetSeriesFailure()
    {
        _tempDownloadsRoot = Path.Combine(Path.GetTempPath(), $"ruvarr-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDownloadsRoot);

        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settings = new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            EpisodeDownloadDirectory: "episodes")
        {
            DownloadsRoot = _tempDownloadsRoot
        };
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private static async Task<DownloadQueueItem> SeedMatchedEpisodeAsync(RuvarrDbContext dbContext)
    {
        TvdbSeries series = new TvdbSeriesBuilder().WithId(5000).Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com/stream"), "Episode 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(series);
        RuvEpisode episode = program.Episodes[0];
        episode.Match(tvdbId: 5001, season: 1, episode: 1, isMissing: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        DownloadQueueItem item = DownloadQueueItem.Create(episode);
        dbContext.Set<DownloadQueueItem>().Add(item);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        return item;
    }

    [Fact]
    public async Task WhenGetSeriesAsyncReturnsFailure_ItemMarkedFailed()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using RuvarrDbContext dbContext = CreateDbContext();
        DownloadQueueItem item = await SeedMatchedEpisodeAsync(dbContext);

        item.MarkDownloading();
        item.MarkDownloaded();
        await dbContext.SaveChangesAsync(cancellationToken);

        string fileName = item.FileName!;
        string completedPath = DownloadFileStore.CompletedPath(_settings, fileName);

        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(ApiClientErrors.RequestFailed));

        SonarrImporter sut = new(_sonarr, dbContext, NullLogger<SonarrImporter>.Instance);

        // Act
        await sut.ImportAsync(item, _settings, fileName, completedPath, cancellationToken);

        // Assert
        item.Status.ShouldBe(DownloadQueueStatus.Failed);
        item.FailureReason.ShouldBe("Sonarr series lookup failed");

        await _sonarr.DidNotReceive().GetManualImportsAsync(
            Arg.Any<string>(),
            Arg.Any<int?>(),
            Arg.Any<CancellationToken>());

        await _sonarr.DidNotReceive().ManualImportFilesAsync(
            Arg.Any<IEnumerable<ManualImportRequest>>(),
            Arg.Any<CancellationToken>());
    }
}
