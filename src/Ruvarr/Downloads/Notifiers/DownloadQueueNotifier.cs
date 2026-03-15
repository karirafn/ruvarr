using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Ruvarr.Downloads.Notifiers;

public sealed class DownloadQueueNotifier
{
    private readonly Lock _lock = new();
    private readonly List<Channel<byte>> _subscribers = [];

    public async IAsyncEnumerable<byte> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<byte> channel = Channel.CreateUnbounded<byte>();
        lock (_lock) _subscribers.Add(channel);
        await using CancellationTokenRegistration registration = cancellationToken.Register(
            () => channel.Writer.TryComplete());

        try
        {
            while (await channel.Reader.WaitToReadAsync(CancellationToken.None))
            {
                while (channel.Reader.TryRead(out byte item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            lock (_lock) _subscribers.Remove(channel);
        }
    }

    public void Notify()
    {
        lock (_lock)
        {
            foreach (Channel<byte> subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(0);
            }
        }
    }
}
