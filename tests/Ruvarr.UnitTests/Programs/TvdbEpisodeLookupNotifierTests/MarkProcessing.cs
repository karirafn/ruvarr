using Ruvarr.Contracts;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.TvdbEpisodeLookupNotifierTests;

public sealed class MarkProcessing
{
    [Fact]
    public void SetsProcessingStatus()
    {
        // Arrange
        TvdbEpisodeLookupNotifier sut = new();
        sut.Enqueue(1, "Program A");

        // Act
        sut.MarkProcessing(1);

        // Assert
        sut.Items.ShouldHaveSingleItem().Status.ShouldBe(TvdbEpisodeLookupStatus.Processing);
    }
}
