using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Programs.Commands.MatchEpisode;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Logging;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.MatchEpisode;

public sealed class SonarrStatusLookupFailure
{
    private const string RuvEpisodeId = "ep0001";
    private const int TvdbSeriesId = 1000;
    private const int TvdbEpisodeId = 5001;
    private const int OtherTvdbEpisodeId = 5002;

    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly RecordingLogger<MatchEpisodeHandler> _logger = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public SonarrStatusLookupFailure()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _tvdb.GetEpisodeAsync(TvdbEpisodeId, Arg.Any<CancellationToken>())
            .Returns(new TvdbEpisodeDataBuilder().WithId(TvdbEpisodeId).Build());
        _tvdb.GetEpisodeAsync(OtherTvdbEpisodeId, Arg.Any<CancellationToken>())
            .Returns(new TvdbEpisodeDataBuilder().WithId(OtherTvdbEpisodeId).WithNumber(2).Build());
        _sonarr.GetSeriesAsync(Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyList<Series>>(ApiClientErrors.RequestFailed));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private async Task SeedAsync(SonarrEpisodeStatus? existingLinkStatus, CancellationToken cancellationToken)
    {
        using RuvarrDbContext seedContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode(RuvEpisodeId, new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(TvdbSeriesId).Build());
        if (existingLinkStatus is SonarrEpisodeStatus status)
        {
            program.Episodes[0].Match(TvdbEpisodeId, season: 1, episode: 1, SonarrStatuses.Of(TvdbEpisodeId, status), MatchOrigin.Automatic);
        }

        seedContext.Set<RuvProgram>().Add(program);
        await seedContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<RuvarrResult> MatchAsync(IReadOnlyList<int> tvdbIds, CancellationToken cancellationToken)
    {
        using RuvarrDbContext dbContext = CreateDbContext();
        MatchEpisodeHandler sut = new(_logger, dbContext, _tvdb, _sonarr);
        return await sut.Handle(new MatchEpisodeCommand(RuvEpisodeId, tvdbIds), cancellationToken);
    }

    private async Task<IReadOnlyList<TvdbEpisode>> LinksAsync(CancellationToken cancellationToken)
    {
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .SingleAsync(x => x.RuvId == RuvEpisodeId, cancellationToken);
        return episode.TvdbEpisodes;
    }

    [Fact]
    public async Task CompletesMatch()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(existingLinkStatus: null, cancellationToken);

        // Act
        RuvarrResult result = await MatchAsync([TvdbEpisodeId], cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StoresNewLinkAsNotTracked()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(existingLinkStatus: null, cancellationToken);

        // Act
        await MatchAsync([TvdbEpisodeId], cancellationToken);

        // Assert
        TvdbEpisode link = (await LinksAsync(cancellationToken)).ShouldHaveSingleItem();
        link.TvdbId.ShouldBe(TvdbEpisodeId);
        link.SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Fact]
    public async Task KeepsStoredStatusOfExistingLink()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(existingLinkStatus: SonarrEpisodeStatus.Downloaded, cancellationToken);

        // Act
        await MatchAsync([TvdbEpisodeId, OtherTvdbEpisodeId], cancellationToken);

        // Assert
        IReadOnlyList<TvdbEpisode> links = await LinksAsync(cancellationToken);
        links.Single(l => l.TvdbId == TvdbEpisodeId).SonarrStatus.ShouldBe(SonarrEpisodeStatus.Downloaded);
        links.Single(l => l.TvdbId == OtherTvdbEpisodeId).SonarrStatus.ShouldBe(SonarrEpisodeStatus.NotTracked);
    }

    [Fact]
    public async Task RecordsManualOrigin()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(existingLinkStatus: SonarrEpisodeStatus.NotTracked, cancellationToken);

        // Act
        await MatchAsync([TvdbEpisodeId], cancellationToken);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>().SingleAsync(x => x.RuvId == RuvEpisodeId, cancellationToken);
        episode.MatchOrigin.ShouldBe(MatchOrigin.Manual);
    }

    [Fact]
    public async Task LogsWarning()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await SeedAsync(existingLinkStatus: null, cancellationToken);

        // Act
        await MatchAsync([TvdbEpisodeId], cancellationToken);

        // Assert
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Sonarr", StringComparison.Ordinal));
    }
}