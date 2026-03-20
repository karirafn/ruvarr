using Ruvarr.Programs.Filters;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramsFilterStateTests;

public sealed class HasSearchText
{
    [Fact]
    public void ReturnsFalseByDefault()
    {
        // Arrange
        ProgramsFilterState sut = new();

        // Act
        bool result = sut.HasSearchText;

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void ReturnsTrueWhenSearchTextIsNonEmpty()
    {
        // Arrange
        ProgramsFilterState sut = new() { SearchText = "test" };

        // Act
        bool result = sut.HasSearchText;

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void ReturnsFalseWhenSearchTextIsWhitespaceOnly()
    {
        // Arrange
        ProgramsFilterState sut = new() { SearchText = "   " };

        // Act
        bool result = sut.HasSearchText;

        // Assert
        result.ShouldBeFalse();
    }
}
