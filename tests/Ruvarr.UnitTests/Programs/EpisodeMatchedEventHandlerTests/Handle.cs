using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.EpisodeMatchedEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task PublishesEventViaBroadcaster()
    {
        // Arrange
        DomainEventBroadcaster broadcaster = new();
        EpisodeMatchedEventHandler sut = new(broadcaster);
        RuvEpisode episode = new RuvEpisodeBuilder().Build();
        EpisodeMatchedEvent @event = new(episode);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        EpisodeMatchedEvent? received = null;
        IAsyncEnumerable<EpisodeMatchedEvent> subscription = broadcaster.Subscribe<EpisodeMatchedEvent>(cts.Token);
        Task watchTask = Task.Run(async () =>
        {
            await foreach (EpisodeMatchedEvent e in subscription)
            {
                received = e;
                await cts.CancelAsync();
            }
        }, TestContext.Current.CancellationToken);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(1000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
        received.ShouldBe(@event);
    }
}
