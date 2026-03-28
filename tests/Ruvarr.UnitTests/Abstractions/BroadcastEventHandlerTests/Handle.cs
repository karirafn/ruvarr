using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.BroadcastEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task PublishesEventViaBroadcaster()
    {
        // Arrange
        DomainEventBroadcaster broadcaster = new();
        BroadcastEventHandler<TestEvent> sut = new(broadcaster);
        TestEvent @event = new("test-value");

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        TestEvent? received = null;
        IAsyncEnumerable<TestEvent> subscription = broadcaster.Subscribe<TestEvent>(cts.Token);
        Task watchTask = Task.Run(async () =>
        {
            await foreach (TestEvent e in subscription)
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

    private sealed record TestEvent(string Value) : IDomainEvent;
}
