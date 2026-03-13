namespace Ruvarr.Abstractions;

internal interface IDomainEventHandler
{
    Task Handle(IDomainEvent @event, CancellationToken cancellationToken);
}

internal interface IDomainEventHandler<in TEvent> : IDomainEventHandler where TEvent : IDomainEvent
{
    Task Handle(TEvent @event, CancellationToken cancellationToken);

    Task IDomainEventHandler.Handle(IDomainEvent @event, CancellationToken cancellationToken) =>
        Handle((TEvent)@event, cancellationToken);
}
