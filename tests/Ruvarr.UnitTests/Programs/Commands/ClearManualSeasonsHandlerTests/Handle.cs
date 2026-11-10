using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.ClearManualSeasons;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.ClearManualSeasonsHandlerTests;

public sealed class Handle : IDisposable
{
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
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ClearsTheSeasonsAndRequestsARefresh()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithMultipleEpisodes().Build();
        program.MatchTvdb(new TvdbSeriesBuilder().Build());
        program.SetManualSeasons([2], [1, 2]);
        program.ClearDomainEvents();
        _dbContext.Set<RuvProgram>().Add(program);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        RuvarrResult result = await new ClearManualSeasonsHandler(_dbContext)
            .Handle(new ClearManualSeasonsCommand(1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        RuvProgram stored = await _dbContext.Set<RuvProgram>().AsNoTracking()
            .SingleAsync(x => x.RuvId == 1, TestContext.Current.CancellationToken);
        stored.ManualSeasons.ShouldBeNull();
        stored.MatchRecheckRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task ReturnsProgramNotFoundForAnUnknownProgram()
    {
        // Act
        RuvarrResult result = await new ClearManualSeasonsHandler(_dbContext)
            .Handle(new ClearManualSeasonsCommand(999), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
    }
}
