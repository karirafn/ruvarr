namespace Ruvarr.Abstractions;

public interface IQueueLease : IDisposable
{
    int RuvId { get; }
}
