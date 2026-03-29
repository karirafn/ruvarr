namespace Ruvarr.Infrastructure.Ruv;

public interface IRuvStreamInspector
{
    Task<long?> EstimateStreamSizeAsync(Uri m3u8Uri, CancellationToken cancellationToken);
}
