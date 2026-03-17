using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.MatchProgram;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.MatchProgram.MatchProgramHandlerTests;

public sealed class Handle
{
    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly TvdbEpisodeLookupNotifier _episodeLookupNotifier = new();
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

    private MatchProgramHandler CreateHandler(RuvarrDbContext dbContext) => new(
        dbContext, _tvdb);

    private void RegisterEventHandler()
    {
        ProgramMatchedTvdbEventHandler handler = new(_episodeLookupNotifier, new DomainEventBroadcaster());
        _serviceProvider
            .GetService(typeof(IEnumerable<IDomainEventHandler<ProgramMatchedTvdbEvent>>))
            .Returns(new IDomainEventHandler<ProgramMatchedTvdbEvent>[] { handler });
    }

    [Fact]
    public async Task ReturnsValidationErrorWhenTvdbSeriesIdExceedsMaximumAllowed()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 10_000_001), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProgramErrors.TvdbSeriesIdOutOfRange);
        await _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsProgramNotFoundWhenProgramDoesNotExist()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(999, 12345), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
        _episodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReturnsTvdbSeriesNotFoundWhenTvdbClientReturnsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(TvdbErrors.SeriesNotFound);
        _episodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchesProgramAndSavesWhenTvdbSeriesAlreadyExistsInDatabase()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        TvdbSeries existingSeries = new TvdbSeriesBuilder().WithId(12345).WithName("Existing Series").Build();
        dbContext.Set<RuvProgram>().Add(program);
        dbContext.Set<TvdbSeries>().Add(existingSeries);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(12345).WithName("Existing Series").Build();
        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns(seriesData);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        program.Series.ShouldBe(existingSeries);
    }

    [Fact]
    public async Task MatchesProgramAndSavesWhenTvdbSeriesNotInDatabase()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(12345).WithName("New Series").Build();
        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns(seriesData);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        program.Series.ShouldNotBeNull();
        program.Series.TvdbId.ShouldBe(12345);
        program.Series.Name.ShouldBe("New Series");
    }

    [Fact]
    public async Task EnqueuesTvdbEpisodeLookupOnSuccess()
    {
        // Arrange
        RegisterEventHandler();
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Test Program").Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(12345).WithName("Test Series").Build();
        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns(seriesData);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _episodeLookupNotifier.Items.ShouldNotBeEmpty();
        _episodeLookupNotifier.Items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task EnqueuesTvdbEpisodeLookupOnReMatch()
    {
        // Arrange
        RegisterEventHandler();
        using RuvarrDbContext dbContext = CreateDbContext();
        TvdbSeries oldSeries = new TvdbSeriesBuilder().WithId(99999).WithName("Old Series").Build();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Test Program").Build();
        program.MatchTvdb(oldSeries);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(12345).WithName("New Series").Build();
        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns(seriesData);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _episodeLookupNotifier.Items.ShouldNotBeEmpty();
        _episodeLookupNotifier.Items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task UpdatesSlugOnExistingTvdbSeriesOnMatch()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        TvdbSeries existingSeries = new TvdbSeriesBuilder().WithId(12345).WithSlug(null).Build();
        dbContext.Set<RuvProgram>().Add(program);
        dbContext.Set<TvdbSeries>().Add(existingSeries);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        SeriesData seriesData = new TvdbSeriesDataBuilder().WithId(12345).WithName("Existing Series").Build();
        _tvdb.GetSeriesAsync(12345, Arg.Any<CancellationToken>()).Returns(seriesData);

        MatchProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new MatchProgramCommand(1, 12345), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        existingSeries.Slug.ShouldBe("test-series");
    }
}
