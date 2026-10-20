using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv.BuilderTests;

public sealed class RuvTvEpisodeBuilderTests
{
    [Fact]
    public void WhenBuiltWithNoCustomisation_ReturnsWellFormedDto()
    {
        // Arrange
        RuvTvEpisodeBuilder builder = new();

        // Act
        RuvTvEpisode result = builder.Build();

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Id.ShouldNotBeNullOrWhiteSpace(),
            () => result.FirstRun.ShouldNotBe(default),
            () => result.Duration.ShouldBeGreaterThan(0),
            () => result.File.ShouldNotBeNull(),
            () => result.Description.ShouldNotBeEmpty());
    }

    [Fact]
    public void WhenWithIdCalled_SetsId()
    {
        // Arrange
        const string expectedId = "ep-999";

        // Act
        RuvTvEpisode result = new RuvTvEpisodeBuilder()
            .WithId(expectedId)
            .Build();

        // Assert
        result.Id.ShouldBe(expectedId);
    }

    [Fact]
    public void WhenWithTitleCalled_SetsTitle()
    {
        // Arrange
        const string expectedTitle = "My Episode";

        // Act
        RuvTvEpisode result = new RuvTvEpisodeBuilder()
            .WithTitle(expectedTitle)
            .Build();

        // Assert
        result.Title.ShouldBe(expectedTitle);
    }
}
