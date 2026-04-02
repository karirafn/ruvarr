using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class UpdateHasMultipleEpisodes
{
    [Fact]
    public void UpdatesFromFalseToTrue()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithMultipleEpisodes(false).Build();

        // Act
        sut.UpdateHasMultipleEpisodes(true);

        // Assert
        sut.HasMultipleEpisodes.ShouldBeTrue();
    }

    [Fact]
    public void UpdatesFromTrueToFalse()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithMultipleEpisodes(true).Build();

        // Act
        sut.UpdateHasMultipleEpisodes(false);

        // Assert
        sut.HasMultipleEpisodes.ShouldBeFalse();
    }

    [Fact]
    public void DoesNothing_WhenValueIsSame()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithMultipleEpisodes(true).Build();
        sut.ClearDomainEvents();

        // Act
        sut.UpdateHasMultipleEpisodes(true);

        // Assert
        sut.HasMultipleEpisodes.ShouldBeTrue();
        sut.DomainEvents.ShouldBeEmpty();
    }
}
