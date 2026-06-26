using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramDbContextExtensionsTests;

public sealed class FindRuvProgramAsync
{
    private const int ExistingRuvId = 42;

    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public FindRuvProgramAsync()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .UseSnakeCaseNamingConvention()
            .Options,
        _serviceProvider);

    [Fact]
    public async Task WhenProgramExists_ReturnsProgramWithEpisodesAndTvdbEpisodes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(ExistingRuvId)
                .WithMultipleEpisodes()
                .Build();

            program.TryAddEpisode(
                id: "ep-1",
                uri: new Uri("http://ruv.is/ep1"),
                title: "Episode 1",
                description: "First",
                firstRun: DateTime.UtcNow,
                duration: TimeSpan.FromMinutes(30));

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act
        using RuvarrDbContext actContext = CreateDbContext();
        RuvProgram? result = await actContext.FindRuvProgramAsync(ExistingRuvId, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.RuvId.ShouldBe(ExistingRuvId);
        result.Episodes.Count.ShouldBe(1);
        result.Episodes[0].TvdbEpisodes.ShouldNotBeNull();
    }

    [Fact]
    public async Task WhenProgramMissing_ReturnsNull()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        // Act
        using RuvarrDbContext actContext = CreateDbContext();
        RuvProgram? result = await actContext.FindRuvProgramAsync(ExistingRuvId, cancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task WhenProgramExists_IsTracked()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        using (RuvarrDbContext seedContext = CreateDbContext())
        {
            await seedContext.Database.EnsureCreatedAsync(cancellationToken);

            RuvProgram program = new RuvProgramBuilder()
                .WithRuvId(ExistingRuvId)
                .WithMultipleEpisodes()
                .Build();

            seedContext.Set<RuvProgram>().Add(program);
            await seedContext.SaveChangesAsync(cancellationToken);
        }

        // Act
        using RuvarrDbContext actContext = CreateDbContext();
        RuvProgram? result = await actContext.FindRuvProgramAsync(ExistingRuvId, cancellationToken);

        // Assert
        result.ShouldNotBeNull();
        actContext.Entry(result).State.ShouldNotBe(Microsoft.EntityFrameworkCore.EntityState.Detached);
    }
}
