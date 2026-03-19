using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramCreatedEventHandlerTests;

public sealed class Handle
{
    [Fact]
    public async Task PublishesEventViaBroadcaster()
    {
        // Arrange
        DomainEventBroadcaster broadcaster = new();
        ProgramCreatedEventHandler sut = new(broadcaster);
        RuvProgram program = new RuvProgramBuilder().Build();
        ProgramCreatedEvent @event = new(program);

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        ProgramCreatedEvent? received = null;
        IAsyncEnumerable<ProgramCreatedEvent> subscription = broadcaster.Subscribe<ProgramCreatedEvent>(cts.Token);
        Task watchTask = Task.Run(async () =>
        {
            await foreach (ProgramCreatedEvent e in subscription)
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
