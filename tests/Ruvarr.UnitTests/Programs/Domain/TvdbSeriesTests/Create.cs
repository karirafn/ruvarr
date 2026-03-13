using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.TvdbSeriesTests;

public sealed class Create
{
    [Fact]
    public void SetsTvdbId()
    {
        // Arrange / Act
        TvdbSeries result = new TvdbSeriesBuilder().WithId("12345").Build();

        // Assert
        result.TvdbId.ShouldBe("12345");
    }

    [Fact]
    public void SetsName()
    {
        // Arrange / Act
        TvdbSeries result = new TvdbSeriesBuilder().WithName("Breaking Bad").Build();

        // Assert
        result.Name.ShouldBe("Breaking Bad");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowsWhenIdIsNullOrWhiteSpace(string? id)
    {
        // Arrange / Act
        Action act = () => TvdbSeries.Create(id!, "Breaking Bad");

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ThrowsWhenNameIsNullOrWhiteSpace(string? name)
    {
        // Arrange / Act
        Action act = () => TvdbSeries.Create("12345", name!);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void InitializesWithNoPrograms()
    {
        // Arrange / Act
        TvdbSeries result = new TvdbSeriesBuilder().Build();

        // Assert
        result.Programs.ShouldBeEmpty();
    }
}
