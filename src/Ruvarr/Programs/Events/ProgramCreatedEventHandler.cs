using Ruvarr.Abstractions;
using Ruvarr.Programs.Notifiers;

namespace Ruvarr.Programs.Events;

internal sealed class ProgramCreatedEventHandler(ProgramCreatedNotifier notifier) : IDomainEventHandler<ProgramCreatedEvent>
{
    public Task Handle(ProgramCreatedEvent @event, CancellationToken cancellationToken)
    {
        notifier.Notify();
        return Task.CompletedTask;
    }
}
