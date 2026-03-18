using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class UpdateImageUrl
{
    [Fact]
    public void SetsImageUrl()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder().Build();
        Uri imageUrl = new("https://example.com/image.jpg");

        // Act
        program.UpdateImageUrl(imageUrl);

        // Assert
        program.ImageUrl.ShouldBe(imageUrl);
    }

    [Fact]
    public void SetsImageUrlToNull()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithImageUrl(new Uri("https://example.com/image.jpg"))
            .Build();

        // Act
        program.UpdateImageUrl(null);

        // Assert
        program.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public void ReplacesExistingImageUrl()
    {
        // Arrange
        RuvProgram program = new RuvProgramBuilder()
            .WithImageUrl(new Uri("https://example.com/old.jpg"))
            .Build();
        Uri newUrl = new("https://example.com/new.jpg");

        // Act
        program.UpdateImageUrl(newUrl);

        // Assert
        program.ImageUrl.ShouldBe(newUrl);
    }
}
