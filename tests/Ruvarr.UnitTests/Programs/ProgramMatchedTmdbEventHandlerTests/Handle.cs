using Ruvarr.Abstractions;
using Ruvarr.Programs.Events;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramMatchedTmdbEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task BroadcastsEvent()
    {
        // Arrange
        DomainEventBroadcaster broadcaster = new();
        ProgramMatchedTmdbEventHandler sut = new(broadcaster);
        ProgramMatchedTmdbEvent @event = new(42, "Test Program");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        ProgramMatchedTmdbEvent? received = null;
        Task watchTask = Task.Run(async () =>
        {
            await foreach (ProgramMatchedTmdbEvent e in broadcaster.Subscribe<ProgramMatchedTmdbEvent>(cts.Token))
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
        received.RuvId.ShouldBe(42);
        received.Name.ShouldBe("Test Program");
    }
}
