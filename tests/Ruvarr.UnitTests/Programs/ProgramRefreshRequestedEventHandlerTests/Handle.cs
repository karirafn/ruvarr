using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs.Events;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbSeriesLookup.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramRefreshRequestedEventHandlerTests;

public sealed class Handle
{
    private readonly ProgramRefreshNotifier _programRefreshNotifier = new();
    private readonly TvdbSeriesLookupNotifier _tvdbSeriesLookupNotifier = new();
    private readonly TvdbEpisodeLookupNotifier _tvdbEpisodeLookupNotifier = new();
    private readonly DomainEventBroadcaster _broadcaster = new();

    private ProgramRefreshRequestedEventHandler CreateSut() => new(
        _programRefreshNotifier, _tvdbSeriesLookupNotifier, _tvdbEpisodeLookupNotifier, _broadcaster);

    [Fact]
    public async Task PriorityEnqueuesProgramRefresh()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        _programRefreshNotifier.Enqueue(10, "Existing");
        ProgramRefreshRequestedEvent @event = new(1, "Target", HasSeries: false, HasMultipleEpisodes: false);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        IReadOnlyList<ProgramRefreshQueueItemSummary> items = _programRefreshNotifier.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task PublishesProgramRefreshQueueChangedEvent()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        ProgramRefreshRequestedEvent @event = new(1, "Test", HasSeries: false, HasMultipleEpisodes: false);

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        QueueChangedEvent<ProgramRefreshQueueItemSummary>? received = null;
        Task watchTask = Task.Run(async () =>
        {
            await foreach (QueueChangedEvent<ProgramRefreshQueueItemSummary> e in _broadcaster.Subscribe<QueueChangedEvent<ProgramRefreshQueueItemSummary>>(cts.Token))
            {
                received = e;
                await cts.CancelAsync();
            }
        }, cts.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
    }

    [Fact]
    public async Task PriorityEnqueuesSeriesLookupWhenProgramHasNoSeries()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        ProgramRefreshRequestedEvent @event = new(1, "Test", HasSeries: false, HasMultipleEpisodes: true);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _tvdbSeriesLookupNotifier.Items.ShouldHaveSingleItem();
        _tvdbSeriesLookupNotifier.Items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotEnqueueSeriesLookupWhenProgramHasSeries()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        ProgramRefreshRequestedEvent @event = new(1, "Test", HasSeries: true, HasMultipleEpisodes: true);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _tvdbSeriesLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PriorityEnqueuesEpisodeLookupWhenProgramHasSeriesAndMultipleEpisodes()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        _tvdbEpisodeLookupNotifier.Enqueue(10, "Existing");
        ProgramRefreshRequestedEvent @event = new(1, "Target", HasSeries: true, HasMultipleEpisodes: true);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> items = _tvdbEpisodeLookupNotifier.Items;
        items.Count.ShouldBe(2);
        items[0].RuvId.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotEnqueueEpisodeLookupWhenProgramHasNoSeries()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        ProgramRefreshRequestedEvent @event = new(1, "Test", HasSeries: false, HasMultipleEpisodes: true);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _tvdbEpisodeLookupNotifier.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task DoesNotEnqueueEpisodeLookupWhenProgramHasSingleEpisode()
    {
        // Arrange
        ProgramRefreshRequestedEventHandler sut = CreateSut();
        ProgramRefreshRequestedEvent @event = new(1, "Test", HasSeries: true, HasMultipleEpisodes: false);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        _tvdbEpisodeLookupNotifier.Items.ShouldBeEmpty();
    }
}
