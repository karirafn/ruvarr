using Ruvarr.Contracts;

namespace Ruvarr.Abstractions;

public abstract class QueueNotifier<TItem> where TItem : notnull, IQueueItemSummary
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, (TItem Item, LinkedListNode<int> Node)> _items = [];
    private readonly LinkedList<int> _order = new();
    private readonly HashSet<int> _read = [];

    public void Enqueue(int ruvId, string programName)
    {
        lock (_lock)
        {
            if (_items.ContainsKey(ruvId))
            {
                return;
            }

            LinkedListNode<int> node = _order.AddLast(ruvId);
            _items[ruvId] = (CreatePending(ruvId, programName), node);
        }
    }

    public void PriorityEnqueue(int ruvId, string programName)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(ruvId, out (TItem Item, LinkedListNode<int> Node) entry))
            {
                if (entry.Item.IsProcessing)
                {
                    return;
                }

                _order.Remove(entry.Node);
                LinkedListNode<int> node = _order.AddFirst(ruvId);
                _items[ruvId] = (entry.Item, node);
                _read.Remove(ruvId);
                return;
            }

            LinkedListNode<int> newNode = _order.AddFirst(ruvId);
            _items[ruvId] = (CreatePending(ruvId, programName), newNode);
        }
    }

    public void MarkProcessing(int ruvId)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(ruvId, out (TItem Item, LinkedListNode<int> Node) entry))
            {
                _items[ruvId] = (WithProcessingStatus(entry.Item), entry.Node);
            }
        }
    }

    public void MarkComplete(int ruvId)
    {
        lock (_lock)
        {
            if (_items.Remove(ruvId, out (TItem Item, LinkedListNode<int> Node) entry))
            {
                _order.Remove(entry.Node);
            }

            _read.Remove(ruvId);
        }
    }

    public IReadOnlyList<TItem> Items
    {
        get
        {
            lock (_lock)
            {
                List<TItem> processing = [];
                List<TItem> pending = [];

                LinkedListNode<int>? current = _order.First;
                while (current is not null)
                {
                    if (_items.TryGetValue(current.Value, out (TItem Item, LinkedListNode<int> Node) entry))
                    {
                        if (entry.Item.IsProcessing)
                        {
                            processing.Add(entry.Item);
                        }
                        else
                        {
                            pending.Add(entry.Item);
                        }
                    }

                    current = current.Next;
                }

                processing.AddRange(pending);
                return processing;
            }
        }
    }

    protected abstract TItem CreatePending(int ruvId, string programName);

    protected abstract TItem WithProcessingStatus(TItem item);

    protected bool TryReadNext(out int ruvId)
    {
        lock (_lock)
        {
            LinkedListNode<int>? current = _order.First;
            while (current is not null)
            {
                if (_items.ContainsKey(current.Value) && _read.Add(current.Value))
                {
                    ruvId = current.Value;
                    return true;
                }

                current = current.Next;
            }

            ruvId = default;
            return false;
        }
    }
}
