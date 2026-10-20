using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv.BuilderTests;

public sealed class RuvTvProgramBuilderTests
{
    [Fact]
    public void WhenBuiltWithNoCustomisation_ReturnsWellFormedDto()
    {
        // Arrange
        RuvTvProgramBuilder builder = new();

        // Act
        RuvTvProgram result = builder.Build();

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Id.ShouldBeGreaterThan(0),
            () => result.Title.ShouldNotBeNullOrWhiteSpace(),
            () => result.Channel.ShouldNotBeNullOrWhiteSpace(),
            () => result.Description.ShouldNotBeEmpty(),
            () => result.Episodes.ShouldNotBeNull());
    }

    [Fact]
    public void WhenWithIdCalled_SetsId()
    {
        // Arrange
        const int expectedId = 12345;

        // Act
        RuvTvProgram result = new RuvTvProgramBuilder()
            .WithId(expectedId)
            .Build();

        // Assert
        result.Id.ShouldBe(expectedId);
    }

    [Fact]
    public void WhenWithEpisodesCalled_SetsEpisodes()
    {
        // Arrange
        RuvTvEpisode episode = new RuvTvEpisodeBuilder()
            .WithId("ep-1")
            .Build();

        // Act
        RuvTvProgram result = new RuvTvProgramBuilder()
            .WithEpisodes([episode])
            .Build();

        // Assert
        result.Episodes.ShouldContain(ep => ep.Id == "ep-1");
    }

    [Fact]
    public void WhenWithChannelCalled_SetsChannel()
    {
        // Arrange
        const string expectedChannel = "ruv2";

        // Act
        RuvTvProgram result = new RuvTvProgramBuilder()
            .WithChannel(expectedChannel)
            .Build();

        // Assert
        result.Channel.ShouldBe(expectedChannel);
    }
}
