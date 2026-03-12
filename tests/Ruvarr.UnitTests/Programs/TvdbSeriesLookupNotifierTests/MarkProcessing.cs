using Ruvarr.Contracts;
using Ruvarr.Programs;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbSeriesLookupNotifierTests;

public sealed class MarkProcessing
{
    [Fact]
    public void SetsProcessingStatus()
    {
        // Arrange
        TvdbSeriesLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkProcessing(1);

        // Assert
        sut.Items.ShouldHaveSingleItem().Status.ShouldBe(TvdbSeriesLookupStatus.Processing);
    }
}
