using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.SetManualSeasons;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.SetManualSeasonsHandlerTests;

public sealed class Handle : IDisposable
{
    private const int SeriesId = 1000;

    private readonly ITvdbClient _tvdb = Substitute.For<ITvdbClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly RuvarrDbContext _dbContext;

    public Handle()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
        _dbContext = new RuvarrDbContext(
            new DbContextOptionsBuilder<RuvarrDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options,
            _serviceProvider);
        _tvdb.GetSeriesAsync(SeriesId, Arg.Any<CancellationToken>()).Returns(new TvdbSeriesDataBuilder()
            .WithId(SeriesId)
            .WithEpisodes(
                new TvdbEpisodeDataBuilder().WithId(1).WithSeasonNumber(0).WithNumber(1).Build(),
                new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build(),
                new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build())
            .Build());
    }

    public void Dispose() => _dbContext.Dispose();

    private SetManualSeasonsHandler CreateSut() => new(_dbContext, _tvdb);

    private async Task<RuvProgram> SeedAsync(bool matched = true)
    {
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        if (matched)
        {
            program.MatchTvdb(new TvdbSeriesBuilder().WithId(SeriesId).Build());
        }

        program.ClearDomainEvents();
        _dbContext.Set<RuvProgram>().Add(program);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return program;
    }

    private Task<RuvProgram> ReloadAsync() =>
        _dbContext.Set<RuvProgram>().AsNoTracking().SingleAsync(x => x.RuvId == 1, TestContext.Current.CancellationToken);

    [Fact]
    public async Task StoresTheSeasonsAndRequestsARefresh()
    {
        // Arrange
        await SeedAsync();

        // Act
        RuvarrResult result = await CreateSut().Handle(new SetManualSeasonsCommand(1, [2, 1]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RuvProgram stored = await ReloadAsync();
        stored.ManualSeasons!.Numbers.ShouldBe([1, 2]);
        stored.MatchRecheckRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task RejectsASeasonTheSeriesDoesNotHave()
    {
        // Arrange
        await SeedAsync();

        // Act
        RuvarrResult result = await CreateSut().Handle(new SetManualSeasonsCommand(1, [3]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonNotInSeries);
        RuvProgram stored = await ReloadAsync();
        stored.ManualSeasons.ShouldBeNull();
        stored.MatchRecheckRequested.ShouldBeFalse();
    }

    [Fact]
    public async Task RejectsAProgramWithoutATvdbSeries()
    {
        // Arrange
        await SeedAsync(matched: false);

        // Act
        RuvarrResult result = await CreateSut().Handle(new SetManualSeasonsCommand(1, [1]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ManualSeasonsRequireSeries);
        await _tvdb.DidNotReceive().GetSeriesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsSeriesNotFoundWhenTvdbHasNoSeriesData()
    {
        // Arrange
        await SeedAsync();
        _tvdb.GetSeriesAsync(SeriesId, Arg.Any<CancellationToken>()).Returns((SeriesData?)null);

        // Act
        RuvarrResult result = await CreateSut().Handle(new SetManualSeasonsCommand(1, [1]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(TvdbErrors.SeriesNotFound);
    }

    [Fact]
    public async Task ReturnsProgramNotFoundForAnUnknownProgram()
    {
        // Act
        RuvarrResult result = await CreateSut().Handle(new SetManualSeasonsCommand(999, [1]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
    }
}
