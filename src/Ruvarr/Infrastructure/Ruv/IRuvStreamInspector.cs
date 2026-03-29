namespace Ruvarr.Infrastructure.Ruv;

public interface IRuvStreamInspector
{
    Task<StreamSizeEstimate?> EstimateStreamSizeAsync(Uri m3u8Uri, CancellationToken cancellationToken);
}
