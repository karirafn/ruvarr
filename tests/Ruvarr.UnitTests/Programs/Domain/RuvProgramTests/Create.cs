using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class Create
{
    [Fact]
    public void SetsRuvId()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithRuvId(42).Build();

        // Assert
        result.RuvId.ShouldBe(42);
    }

    [Fact]
    public void SetsChannel()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithChannel("ruv").Build();

        // Assert
        result.Channel.ShouldBe("ruv");
    }

    [Fact]
    public void SetsName()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithName("Fréttir").Build();

        // Assert
        result.Name.ShouldBe("Fréttir");
    }

    [Fact]
    public void SetsForeignName()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithForeignName("News").Build();

        // Assert
        result.ForeignName.ShouldBe("News");
    }

    [Fact]
    public void SetsHasMultipleEpisodes()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithMultipleEpisodes(true).Build();

        // Assert
        result.HasMultipleEpisodes.ShouldBeTrue();
    }

    [Fact]
    public void SetsCreatedToUtcNow()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        RuvProgram result = new RuvProgramBuilder().Build();

        // Assert
        result.Created.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void SetsSlug()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithSlug("frettir").Build();

        // Assert
        result.Slug.ShouldBe("frettir");
    }

    [Fact]
    public void SlugIsNullByDefault()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().Build();

        // Assert
        result.Slug.ShouldBeNull();
    }

    [Fact]
    public void ValidSlugPassesThroughUnchanged()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithSlug("stjornubio").Build();

        // Assert
        result.Slug.ShouldBe("stjornubio");
    }

    [Fact]
    public void InvalidSlugIsStoredAsNull()
    {
        // Arrange / Act
        RuvProgram result = new RuvProgramBuilder().WithSlug("../../evil").Build();

        // Assert
        result.Slug.ShouldBeNull();
    }
}
