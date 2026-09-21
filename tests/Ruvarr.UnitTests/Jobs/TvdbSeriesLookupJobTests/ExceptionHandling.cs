using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbSeriesLookup.Jobs;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Jobs.TvdbSeriesLookupJobTests;

public sealed class ExceptionHandling
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly TvdbSeriesLookupNotifier _lookupQueue = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IJobExecutionContext _context = Substitute.For<IJobExecutionContext>();
    private readonly ISettingsStore _settingsStore = Substitute.For<ISettingsStore>();

    public ExceptionHandling()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _context.CancellationToken.Returns(CancellationToken.None);
        _settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://sonarr", SonarrApiKey: "key",
            TvdbApiKey: "tvdb-key", TmdbApiKey: "tmdb-key"));
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private TvdbSeriesLookupJob CreateJob(RuvarrDbContext dbContext) => new(
        NullLogger<TvdbSeriesLookupJob>.Instance,
        dbContext,
        _tvdb,
        _lookupQueue,
        new DomainEventBroadcaster(),
        _settingsStore);

    [Fact]
    public async Task MarksComplete_WhenTvdbClientThrows()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(1)
            .WithName("Test Program")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.SearchAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("TVDB API unavailable"));
        _lookupQueue.Enqueue(1, program.Name);
        _lookupQueue.Items.ShouldHaveSingleItem();
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        _lookupQueue.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task WhenCancelled_PropagatesOperationCanceledException()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder()
            .WithRuvId(2)
            .WithName("Test Program")
            .WithForeignName(null)
            .Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        _tvdb.SearchAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int?>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        _lookupQueue.Enqueue(2, program.Name);
        TvdbSeriesLookupJob sut = CreateJob(dbContext);

        // Act
        Func<Task> act = async () => await sut.Execute(_context, TestContext.Current.CancellationToken);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
