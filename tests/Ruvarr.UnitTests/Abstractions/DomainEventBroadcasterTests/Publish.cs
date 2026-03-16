using Ruvarr.Abstractions;

using Shouldly;

namespace Ruvarr.UnitTests.Abstractions.DomainEventBroadcasterTests;

public sealed class Publish
{
    private sealed record TestEventWithValue(string Value);

    [Fact]
    public async Task DeliversEventToSubscriber()
    {
        // Arrange
        DomainEventBroadcaster sut = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        TestEventWithValue? received = null;
        Task subscribeTask = Task.Run(async () =>
        {
            await foreach (TestEventWithValue e in sut.Subscribe<TestEventWithValue>(cts.Token))
            {
                received = e;
                await cts.CancelAsync();
            }
        }, cts.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        sut.Publish(new TestEventWithValue("hello"));

        // Assert
        await Task.WhenAny(subscribeTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
        received.Value.ShouldBe("hello");
    }

    [Fact]
    public async Task DeliversEventToMultipleSubscribers()
    {
        // Arrange
        DomainEventBroadcaster sut = new();
        using CancellationTokenSource cts1 = new(TimeSpan.FromSeconds(5));
        using CancellationTokenSource cts2 = new(TimeSpan.FromSeconds(5));

        TestEventWithValue? received1 = null;
        TestEventWithValue? received2 = null;

        Task sub1 = Task.Run(async () =>
        {
            await foreach (TestEventWithValue e in sut.Subscribe<TestEventWithValue>(cts1.Token))
            {
                received1 = e;
                await cts1.CancelAsync();
            }
        }, cts1.Token);

        Task sub2 = Task.Run(async () =>
        {
            await foreach (TestEventWithValue e in sut.Subscribe<TestEventWithValue>(cts2.Token))
            {
                received2 = e;
                await cts2.CancelAsync();
            }
        }, cts2.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        sut.Publish(new TestEventWithValue("multi"));

        // Assert
        await Task.WhenAny(Task.WhenAll(sub1, sub2), Task.Delay(2000, TestContext.Current.CancellationToken));
        received1.ShouldNotBeNull();
        received2.ShouldNotBeNull();
    }

    [Fact]
    public void WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        DomainEventBroadcaster sut = new();

        // Act & Assert
        Should.NotThrow(() => sut.Publish(new TestEventWithValue("test")));
    }

    private sealed record EventA(int Id);
    private sealed record EventB(int Id);

    [Fact]
    public async Task DoesNotCrossDeliverDifferentEventTypes()
    {
        // Arrange
        DomainEventBroadcaster sut = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

        EventA? received = null;
        _ = Task.Run(async () =>
        {
            await foreach (EventA e in sut.Subscribe<EventA>(cts.Token))
            {
                received = e;
                await cts.CancelAsync();
            }
        }, cts.Token);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        sut.Publish(new EventB(1));

        // Assert
        await Task.Delay(200, TestContext.Current.CancellationToken);
        received.ShouldBeNull();
        await cts.CancelAsync();
    }

    [Fact]
    public async Task CancellationRemovesSubscriber()
    {
        // Arrange
        DomainEventBroadcaster sut = new();
        using CancellationTokenSource cts = new();

        bool completed = false;
        Task subscribeTask = Task.Run(async () =>
        {
            int count = 0;
            await foreach (TestEventWithValue _ in sut.Subscribe<TestEventWithValue>(cts.Token))
            {
                count++;
            }
            completed = count == 0;
        }, CancellationToken.None);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        await cts.CancelAsync();

        // Assert
        await Task.WhenAny(subscribeTask, Task.Delay(2000, TestContext.Current.CancellationToken));
        completed.ShouldBeTrue();
    }
}
