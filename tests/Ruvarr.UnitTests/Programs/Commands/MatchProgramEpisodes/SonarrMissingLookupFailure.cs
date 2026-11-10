using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Programs.Commands.MatchProgramEpisodes;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Logging;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.MatchProgramEpisodes;

public sealed class SonarrMissingLookupFailure
{
    private const int RuvProgramId = 1;
    private const int TvdbSeriesId = 1000;
    private const int TvdbEpisodeId = 5001;

    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly RecordingLogger<MatchProgramEpisodesHandler> _logger = new();
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public SonarrMissingLookupFailure()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _tvdb.GetSeriesAsync(TvdbSeriesId, Arg.Any<CancellationToken>())
            .Returns(new TvdbSeriesDataBuilder()
                .WithId(TvdbSeriesId)
                .WithEpisodes(new TvdbEpisodeDataBuilder().WithId(TvdbEpisodeId).WithSeasonNumber(1).WithNumber(1).Build())
                .Build());
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>(ApiClientErrors.RequestFailed));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options,
        _serviceProvider);

    private async Task<RuvarrResult> MatchAsync(CancellationToken cancellationToken)
    {
        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            RuvProgram program = new RuvProgramBuilder().WithRuvId(RuvProgramId).Build();
            program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
            program.MatchTvdb(new TvdbSeriesBuilder().WithId(TvdbSeriesId).Build());
            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        using RuvarrDbContext dbContext = CreateDbContext();
        MatchProgramEpisodesHandler sut = new(_logger, dbContext, _tvdb, _sonarr);
        return await sut.Handle(new MatchProgramEpisodesCommand(RuvProgramId), cancellationToken);
    }

    [Fact]
    public async Task CompletesMatch()
    {
        // Act
        RuvarrResult result = await MatchAsync(TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StoresLinksAsNotMissing()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await MatchAsync(cancellationToken);

        // Assert
        using RuvarrDbContext assertContext = CreateDbContext();
        RuvEpisode episode = await assertContext.Set<RuvEpisode>()
            .Include(x => x.TvdbEpisodes)
            .SingleAsync(cancellationToken);
        TvdbEpisode link = episode.TvdbEpisodes.ShouldHaveSingleItem();
        link.TvdbId.ShouldBe(TvdbEpisodeId);
        link.IsMissing.ShouldBeFalse();
    }

    [Fact]
    public async Task LogsWarning()
    {
        // Act
        await MatchAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Sonarr", StringComparison.Ordinal));
    }
}
