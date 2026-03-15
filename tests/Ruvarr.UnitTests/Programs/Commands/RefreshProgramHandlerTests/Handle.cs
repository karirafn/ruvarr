using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Domain;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.RefreshProgramHandlerTests;

public sealed class Handle
{
    private readonly TvdbEpisodeLookupNotifier _tvdbEpisodeLookupNotifier = new();
    private readonly ProgramRefreshNotifier _programRefreshNotifier = new();
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
        dbContext, _programRefreshNotifier, _tvdbEpisodeLookupNotifier);

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
    public async Task DoesNotEnqueueWhenProgramHasNoSeries()
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
    public async Task DoesNotEnqueueWhenProgramHasSingleEpisode()
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
}
