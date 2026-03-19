using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Ruvarr.Abstractions;

internal sealed class DomainEventBroadcaster : IDomainEventBroadcaster
{
    private readonly ConcurrentDictionary<Type, List<object>> _subscribers = new();
    private readonly Lock _lock = new();

    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        if (!_subscribers.TryGetValue(typeof(TEvent), out List<object>? channels))
        {
            return;
        }

        lock (_lock)
        {
            foreach (object channel in channels)
            {
                ((Channel<TEvent>)channel).Writer.TryWrite(@event);
            }
        }
    }

    public IAsyncEnumerable<TEvent> Subscribe<TEvent>(CancellationToken cancellationToken) where TEvent : class
    {
        Channel<TEvent> channel = Channel.CreateUnbounded<TEvent>();
        List<object> channels = _subscribers.GetOrAdd(typeof(TEvent), _ => []);

        lock (_lock)
        {
            channels.Add(channel);
        }

        return ReadChannel(channel, channels, _lock, cancellationToken);
    }

    private static async IAsyncEnumerable<TEvent> ReadChannel<TEvent>(
        Channel<TEvent> channel,
        List<object> channels,
        Lock @lock,
        [EnumeratorCancellation] CancellationToken cancellationToken) where TEvent : class
    {
        await using CancellationTokenRegistration registration = cancellationToken.Register(
            () => channel.Writer.TryComplete());

        try
        {
            while (await channel.Reader.WaitToReadAsync(CancellationToken.None))
            {
                while (channel.Reader.TryRead(out TEvent? item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            lock (@lock)
            {
                channels.Remove(channel);
            }
        }
    }
}
