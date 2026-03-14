using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class UpdateSlug
{
    [Fact]
    public void UpdatesSlug()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithSlug("old-slug").Build();

        // Act
        program.UpdateSlug("new-slug");

        // Assert
        program.Slug.ShouldBe("new-slug");
    }

    [Fact]
    public void SetsSlugToNull()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().WithSlug("some-slug").Build();

        // Act
        program.UpdateSlug(null);

        // Assert
        program.Slug.ShouldBeNull();
    }

    [Fact]
    public void ValidSlugPassesThroughUnchanged()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();

        // Act
        program.UpdateSlug("stjornubio");

        // Assert
        program.Slug.ShouldBe("stjornubio");
    }

    [Fact]
    public void InvalidSlugIsStoredAsNull()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();

        // Act
        program.UpdateSlug("../../evil");

        // Assert
        program.Slug.ShouldBeNull();
    }
}
