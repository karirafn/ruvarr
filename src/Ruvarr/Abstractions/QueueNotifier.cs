using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Ruvarr.Contracts;

namespace Ruvarr.Abstractions;

public abstract class QueueNotifier<TItem> where TItem : notnull, IQueueItemSummary
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, TItem> _items = [];
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

            _items[ruvId] = CreatePending(ruvId, programName);
        }

        _channel.Writer.TryWrite(ruvId);
        Notify();
    }

    public void MarkProcessing(int ruvId)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(ruvId, out TItem? item))
            {
                _items[ruvId] = WithProcessingStatus(item);
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

    public IReadOnlyList<TItem> Items
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

    protected abstract TItem CreatePending(int ruvId, string programName);

    protected abstract TItem WithProcessingStatus(TItem item);

    protected bool TryReadNext(out int ruvId) => _channel.Reader.TryRead(out ruvId);

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