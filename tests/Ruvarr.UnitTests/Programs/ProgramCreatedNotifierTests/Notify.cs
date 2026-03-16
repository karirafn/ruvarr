using Ruvarr.Programs.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramCreatedNotifierTests;

public sealed class Notify
{
    [Fact]
    public async Task Notify_SignalsAllSubscribers()
    {
        // Arrange
        ProgramCreatedNotifier sut = new();
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        byte? received = null;
        Task watchTask = Task.Run(async () =>
        {
            await foreach (byte b in sut.WatchAsync(cts.Token))
            {
                received = b;
                await cts.CancelAsync();
            }
        }, TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Act
        sut.Notify();

        // Assert
        await Task.WhenAny(watchTask, Task.Delay(1000, TestContext.Current.CancellationToken));
        received.ShouldNotBeNull();
    }

    [Fact]
    public void Notify_WithNoSubscribers_DoesNotThrow()
    {
        // Arrange
        ProgramCreatedNotifier sut = new();

        // Act & Assert
        Should.NotThrow(() => sut.Notify());
    }
}
