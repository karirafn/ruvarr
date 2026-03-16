using Ruvarr.Programs.Events;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeAddedToMatchedProgramEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task EnqueuesTvdbEpisodeLookup()
    {
        // Arrange
        TvdbEpisodeLookupNotifier notifier = new();
        EpisodeAddedToMatchedProgramEventHandler sut = new(notifier);
        EpisodeAddedToMatchedProgramEvent @event = new(42, "Test Program");

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        notifier.Items.ShouldHaveSingleItem();
        notifier.Items[0].RuvId.ShouldBe(42);
        notifier.Items[0].ProgramName.ShouldBe("Test Program");
    }
}
