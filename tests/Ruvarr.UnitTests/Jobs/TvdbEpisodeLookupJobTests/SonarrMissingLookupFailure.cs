using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.Testing.Logging;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbEpisodeLookupJobTests;

public sealed class SonarrMissingLookupFailure
{
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly ISonarrClient _sonarr = Substitute.For<ISonarrClient>();
    private readonly ITvdbEpisodeMatcher _matcher = Substitute.For<ITvdbEpisodeMatcher>();
    private readonly TvdbEpisodeLookupNotifier _notifier = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();
    private readonly RecordingLogger<TvdbEpisodeLookupJob> _logger = new();

    public SonarrMissingLookupFailure()
    {
        _sonarr.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>(ApiClientErrors.RequestFailed));
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
    }

    private async Task RunLookupAsync(CancellationToken cancellationToken)
    {
        using RuvarrDbContext dbContext = new(
            new DbContextOptionsBuilder<RuvarrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            _serviceProvider);
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        program.MatchTvdb(new TvdbSeriesBuilder().WithId(1000).Build());
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(1000).Build();
        _tvdb.GetSeriesAsync(1000, Arg.Any<CancellationToken>()).Returns(seriesData);
        _notifier.Enqueue(1, program.Name);

        TvdbEpisodeLookupJob sut = new(
            _logger, dbContext, _tvdb, _sonarr, _notifier, new DomainEventBroadcaster(), _settingsStore, _matcher);
        await sut.Execute(_context, cancellationToken);
    }

    [Fact]
    public async Task MatchesWithNoEpisodesMissing()
    {
        // Act
        await RunLookupAsync(TestContext.Current.CancellationToken);

        // Assert
        await _matcher.Received(1).MatchAsync(
            Arg.Is<EpisodeMatchingContext>(ctx => ctx.MissingTvdbIds.Count == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogsWarning()
    {
        // Act
        await RunLookupAsync(TestContext.Current.CancellationToken);

        // Assert
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("Sonarr", StringComparison.Ordinal));
    }
}
