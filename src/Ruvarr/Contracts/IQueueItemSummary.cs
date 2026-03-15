namespace Ruvarr.Contracts;

public interface IQueueItemSummary
{
    string ProgramName { get; }
    bool IsProcessing { get; }
}