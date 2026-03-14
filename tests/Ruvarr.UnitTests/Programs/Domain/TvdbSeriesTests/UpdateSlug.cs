using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.TvdbSeriesTests;

public sealed class UpdateSlug
{
    [Fact]
    public void SetsSlugWhenPreviouslyNull()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder().WithSlug(null).Build();

        // Act
        series.UpdateSlug("breaking-bad");

        // Assert
        series.Slug.ShouldBe("breaking-bad");
    }

    [Fact]
    public void UpdatesExistingSlug()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder().WithSlug("old-slug").Build();

        // Act
        series.UpdateSlug("new-slug");

        // Assert
        series.Slug.ShouldBe("new-slug");
    }

    [Fact]
    public void SetsSlugToNull()
    {
        // Arrange
        TvdbSeries series = new TvdbSeriesBuilder().WithSlug("breaking-bad").Build();

        // Act
        series.UpdateSlug(null);

        // Assert
        series.Slug.ShouldBeNull();
    }
}
