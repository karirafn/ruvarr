using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Ruvarr.Contracts;

namespace Ruvarr.Programs;

public sealed class TvdbEpisodeLookupNotifier
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, TvdbEpisodeLookupQueueItemSummary> _items = [];
    private readonly List<Channel<byte>> _subscribers = [];
    private readonly Channel<int> _channel = Channel.CreateUnbounded<int>();

    public void Enqueue(int ruvId, string programName)
    {
        lock (_lock)
        {
            if (_items.ContainsKey(ruvId))
            {
                return;
            }

            _items[ruvId] = new TvdbEpisodeLookupQueueItemSummary(ruvId, programName, TvdbEpisodeLookupStatus.Pending);
        }

        _channel.Writer.TryWrite(ruvId);
        Notify();
    }

    public bool TryDequeue(out int ruvId)
    {
        if (_channel.Reader.TryRead(out ruvId))
        {
            return true;
        }

        return false;
    }

    public void MarkProcessing(int ruvId)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(ruvId, out TvdbEpisodeLookupQueueItemSummary? item))
            {
                _items[ruvId] = item with { Status = TvdbEpisodeLookupStatus.Processing };
            }
        }

        Notify();
    }

    public void MarkComplete(int ruvId)
    {
        lock (_lock)
        {
            _items.Remove(ruvId);
        }

        Notify();
    }

    public IReadOnlyList<TvdbEpisodeLookupQueueItemSummary> Items
    {
        get
        {
            lock (_lock)
            {
#pragma warning disable CA1309
                return [.. _items.Values.OrderBy(x => x.ProgramName, StringComparer.Create(new CultureInfo("is-IS"), ignoreCase: true))];
#pragma warning restore CA1309
            }
        }
    }

    public async IAsyncEnumerable<byte> WatchAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Channel<byte> channel = Channel.CreateUnbounded<byte>();
        lock (_lock)
        {
            _subscribers.Add(channel);
        }

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
            lock (_lock)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    private void Notify()
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
