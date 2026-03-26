using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;
using Ruvarr.TvdbEpisodeLookup;

using Shouldly;

namespace Ruvarr.UnitTests.TvdbEpisodeLookup;

public sealed class TvdbEpisodeMatcherTests
{
    [Fact]
    public async Task CallsStrategiesInOrder()
    {
        // Arrange
        List<int> callOrder = [];
        IEpisodeMatchingStrategy strategy1 = Substitute.For<IEpisodeMatchingStrategy>();
        strategy1.MatchAsync(Arg.Any<EpisodeMatchingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add(1));
        IEpisodeMatchingStrategy strategy2 = Substitute.For<IEpisodeMatchingStrategy>();
        strategy2.MatchAsync(Arg.Any<EpisodeMatchingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add(2));
        IEpisodeMatchingStrategy strategy3 = Substitute.For<IEpisodeMatchingStrategy>();
        strategy3.MatchAsync(Arg.Any<EpisodeMatchingContext>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => callOrder.Add(3));

        TvdbEpisodeMatcher sut = new(
            [strategy1, strategy2, strategy3],
            NullLogger<TvdbEpisodeMatcher>.Instance);

        RuvProgram program = new RuvProgramBuilder().Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().Build();
        EpisodeMatchingContext context = new(program, seriesData, []);

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        callOrder.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task PassesContextToEachStrategy()
    {
        // Arrange
        IEpisodeMatchingStrategy strategy = Substitute.For<IEpisodeMatchingStrategy>();

        TvdbEpisodeMatcher sut = new(
            [strategy],
            NullLogger<TvdbEpisodeMatcher>.Instance);

        RuvProgram program = new RuvProgramBuilder().Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().Build();
        HashSet<int> missingIds = [42];
        EpisodeMatchingContext context = new(program, seriesData, missingIds);

        // Act
        await sut.MatchAsync(context, CancellationToken.None);

        // Assert
        await strategy.Received(1).MatchAsync(context, CancellationToken.None);
    }

    [Fact]
    public async Task WorksWithNoStrategies()
    {
        // Arrange
        TvdbEpisodeMatcher sut = new(
            [],
            NullLogger<TvdbEpisodeMatcher>.Instance);

        RuvProgram program = new RuvProgramBuilder().Build();
        SeriesData seriesData = new TvdbSeriesDataBuilder().Build();
        EpisodeMatchingContext context = new(program, seriesData, []);

        // Act
        Func<Task> act = () => sut.MatchAsync(context, CancellationToken.None);

        // Assert
        await act.ShouldNotThrowAsync();
    }
}
