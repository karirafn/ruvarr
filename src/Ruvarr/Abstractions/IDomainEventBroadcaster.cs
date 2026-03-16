namespace Ruvarr.Abstractions;

internal interface IDomainEventBroadcaster
{
    void Publish<TEvent>(TEvent @event) where TEvent : class;
    IAsyncEnumerable<TEvent> Subscribe<TEvent>(CancellationToken cancellationToken) where TEvent : class;
}
