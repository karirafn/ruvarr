using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class SetHasMissingEpisodes
{
    [Fact]
    public void SetHasMissingEpisodes_SetsHasMissingEpisodesToTrue()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.SetHasMissingEpisodes(true);

        // Assert
        sut.HasMissingEpisodes.ShouldBeTrue();
    }

    [Fact]
    public void SetHasMissingEpisodes_SetsHasMissingEpisodesToFalse()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.SetHasMissingEpisodes(true);

        // Act
        sut.SetHasMissingEpisodes(false);

        // Assert
        sut.HasMissingEpisodes.ShouldBeFalse();
    }
}
