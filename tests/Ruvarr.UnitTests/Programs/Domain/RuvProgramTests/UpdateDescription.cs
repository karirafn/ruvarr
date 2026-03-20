using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class UpdateDescription
{
    [Fact]
    public void SetsDescription()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();

        // Act
        program.UpdateDescription("A great show.");

        // Assert
        program.Description.ShouldBe("A great show.");
    }

    [Fact]
    public void SetsDescriptionToNull()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithDescription("Old description")
            .Build();

        // Act
        program.UpdateDescription(null);

        // Assert
        program.Description.ShouldBeNull();
    }

    [Fact]
    public void ReplacesExistingDescription()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithDescription("Old description")
            .Build();

        // Act
        program.UpdateDescription("New description");

        // Assert
        program.Description.ShouldBe("New description");
    }

    [Fact]
    public void TruncatesDescriptionLongerThan4096Characters()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();
        string longDescription = new('x', 5000);

        // Act
        program.UpdateDescription(longDescription);

        // Assert
        program.Description.ShouldNotBeNull();
        program.Description.Length.ShouldBe(4096);
    }
}
