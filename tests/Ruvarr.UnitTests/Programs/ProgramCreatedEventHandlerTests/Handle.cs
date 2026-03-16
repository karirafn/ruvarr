using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Programs.Notifiers;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramCreatedEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task Handle_NotifiesSubscribers()
    {
        // Arrange
        ProgramCreatedNotifier notifier = new();
        ProgramCreatedEventHandler sut = new(notifier);
        RuvProgram program = new RuvProgramBuilder().Build();
        ProgramCreatedEvent @event = new(program);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        byte? received = null;
        Task watchTask = Task.Run(async () =>
        {
            await foreach (byte b in notifier.WatchAsync(cts.Token))
            {
                received = b;
                await cts.CancelAsync();
            }
        }, TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        await sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(1000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
    }
}
