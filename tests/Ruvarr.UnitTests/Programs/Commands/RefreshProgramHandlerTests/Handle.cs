using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.RefreshProgramHandlerTests;

public sealed class Handle
{
    private readonly TvdbEpisodeLookupNotifier _tvdbEpisodeLookupNotifier = new();
    private readonly TvdbSeriesLookupNotifier _tvdbSeriesLookupNotifier = new();
    private readonly ProgramRefreshNotifier _programRefreshNotifier = new();
    private readonly DomainEventBroadcaster _broadcaster = new();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public Handle()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private RefreshProgramHandler CreateHandler(RuvarrDbContext dbContext) => new(
        dbContext, _programRefreshNotifier, _tvdbEpisodeLookupNotifier, _tvdbSeriesLookupNotifier, _broadcaster);

    [Fact]
    public async Task ReturnsProgramNotFoundWhenProgramDoesNotExist()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new RefreshProgramCommand(999), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
        _tvdbEpisodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DoesNotEnqueueEpisodeLookupWhenProgramHasNoSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        _tvdbEpisodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DoesNotEnqueueEpisodeLookupWhenProgramHasSingleEpisode()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes(false).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        _tvdbEpisodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task EnqueuesWhenProgramHasSeriesAndMultipleEpisodes()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program.TryAddEpisode("ep0001", new Uri("http://test.com"), "Þáttur 1", "", DateTime.UtcNow);
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        int episodeLookupCountBefore = program.Episodes[0].LookupCount;
        DateTime? episodeNextLookupBefore = program.Episodes[0].NextLookup;

        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        _tvdbEpisodeLookupNotifier.Items.ShouldNotBeEmpty();
        program.Episodes[0].LookupCount.ShouldBe(episodeLookupCountBefore);
        program.Episodes[0].NextLookup.ShouldBe(episodeNextLookupBefore);
    }

    [Fact]
    public async Task PriorityEnqueuesProgramRefresh()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram existingProgram = new RuvProgramBuilder().WithRuvId(10).WithName("Existing").Build();
        RuvProgram targetProgram = new RuvProgramBuilder().WithRuvId(1).WithName("Target").Build();
        dbContext.Set<RuvProgram>().AddRange(existingProgram, targetProgram);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _programRefreshNotifier.Enqueue(10, "Existing");
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = _programRefreshNotifier.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task PriorityEnqueuesSeriesLookupWhenProgramHasNoSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        _tvdbSeriesLookupNotifier.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DoesNotEnqueueSeriesLookupWhenProgramHasSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        _tvdbSeriesLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PriorityEnqueuesEpisodeLookup()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries series = new TvdbSeriesBuilder().Build();
        RuvProgram existingProgram = new RuvProgramBuilder().WithRuvId(10).WithName("Existing").WithMultipleEpisodes().Build();
        existingProgram.MatchTvdb(new TvdbSeriesBuilder().Build());
        RuvProgram targetProgram = new RuvProgramBuilder().WithRuvId(1).WithName("Target").WithMultipleEpisodes().Build();
        targetProgram.TryAddEpisode("ep0001", new Uri("http://test.com"), "Episode 1", "", DateTime.UtcNow);
        targetProgram.MatchTvdb(series);
        dbContext.Set<RuvProgram>().AddRange(existingProgram, targetProgram);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        _tvdbEpisodeLookupNotifier.Enqueue(10, "Existing");
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = _tvdbEpisodeLookupNotifier.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(1);
    }
}
