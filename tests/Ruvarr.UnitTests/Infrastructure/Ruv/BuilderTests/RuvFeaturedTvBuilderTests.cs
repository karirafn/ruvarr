using Ruvarr.Infrastructure.Ruv.Models;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv.BuilderTests;

public sealed class RuvFeaturedTvBuilderTests
{
    [Fact]
    public void WhenBuiltWithNoCustomisation_ReturnsWellFormedDto()
    {
        // Arrange
        RuvFeaturedTvBuilder builder = new();

        // Act
        RuvFeaturedTv result = builder.Build();

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Panels.ShouldNotBeNull(),
            () => result.Panels.ShouldNotBeEmpty(),
            () => result.Panels[0].Programs.ShouldNotBeNull());
    }

    [Fact]
    public void WhenWithProgramsCalled_PutsAllProgramsInSinglePanel()
    {
        // Arrange
        RuvTvProgram program1 = new RuvTvProgramBuilder().WithId(1).Build();
        RuvTvProgram program2 = new RuvTvProgramBuilder().WithId(2).Build();

        // Act
        RuvFeaturedTv result = new RuvFeaturedTvBuilder()
            .WithPrograms(program1, program2)
            .Build();

        // Assert
        result.ShouldSatisfyAllConditions(
            () => result.Panels.Count.ShouldBe(1),
            () => result.Panels[0].Programs.Count.ShouldBe(2),
            () => result.Panels[0].Programs.ShouldContain(p => p.Id == 1),
            () => result.Panels[0].Programs.ShouldContain(p => p.Id == 2));
    }

    [Fact]
    public void WhenBuiltWithPrograms_SelectManyReturnsAllPrograms()
    {
        // Arrange
        RuvTvProgram program1 = new RuvTvProgramBuilder().WithId(10).Build();
        RuvTvProgram program2 = new RuvTvProgramBuilder().WithId(20).Build();
        RuvFeaturedTv sut = new RuvFeaturedTvBuilder()
            .WithPrograms(program1, program2)
            .Build();

        // Act
        IEnumerable<RuvTvProgram> programs = sut.Panels.SelectMany(p => p.Programs);

        // Assert
        programs.ShouldContain(p => p.Id == 10);
        programs.ShouldContain(p => p.Id == 20);
    }
}
