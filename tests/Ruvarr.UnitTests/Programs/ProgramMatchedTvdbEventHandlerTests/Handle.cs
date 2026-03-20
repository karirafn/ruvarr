using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Events;
using Ruvarr.TvdbEpisodeLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramMatchedTvdbEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task EnqueuesTvdbEpisodeLookup()
    {
        // Arrange
        TvdbEpisodeLookupNotifier notifier = new();
        DomainEventBroadcaster broadcaster = new();
        ProgramMatchedTvdbEventHandler sut = new(notifier, broadcaster);
        ProgramMatchedTvdbEvent @event = new(42, "Test Program");

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        notifier.Items.ShouldHaveSingleItem();
        notifier.Items[0].RuvId.ShouldBe(42);
        notifier.Items[0].ProgramName.ShouldBe("Test Program");
    }

    [Fact]
    public async Task BroadcastsProgramMatchedTvdbEvent()
    {
        // Arrange
        TvdbEpisodeLookupNotifier notifier = new();
        DomainEventBroadcaster broadcaster = new();
        ProgramMatchedTvdbEventHandler sut = new(notifier, broadcaster);
        ProgramMatchedTvdbEvent @event = new(42, "Test Program");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        ProgramMatchedTvdbEvent? received = null;
        IAsyncEnumerable<ProgramMatchedTvdbEvent> subscription =
            broadcaster.Subscribe<ProgramMatchedTvdbEvent>(cts.Token);
        Task watchTask = Task.Run(async () =>
        {
            await foreach (ProgramMatchedTvdbEvent e in subscription)
            {
                received = e;
                await cts.CancelAsync();
            }
        }, cts.Token);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
        received.RuvId.ShouldBe(42);
        received.Name.ShouldBe("Test Program");
    }

    [Fact]
    public async Task PublishesQueueChangedEvent()
    {
        // Arrange
        TvdbEpisodeLookupNotifier notifier = new();
        DomainEventBroadcaster broadcaster = new();
        ProgramMatchedTvdbEventHandler sut = new(notifier, broadcaster);
        ProgramMatchedTvdbEvent @event = new(42, "Test Program");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>? received = null;
        IAsyncEnumerable<QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>> subscription =
            broadcaster.Subscribe<QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary>>(cts.Token);
        Task watchTask = Task.Run(async () =>
        {
            await foreach (QueueChangedEvent<TvdbEpisodeLookupQueueItemSummary> e in subscription)
            {
                received = e;
                await cts.CancelAsync();
            }
        }, cts.Token);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
    }
}
