using Ruvarr.Abstractions;
using Ruvarr.Downloads.Events;
using Ruvarr.Infrastructure.FFmpeg;

namespace Ruvarr.Downloads.Notifiers;

internal sealed class DownloadProgressNotifier(IDomainEventBroadcaster broadcaster, TimeProvider timeProvider)
{
    private const int BufferSize = 5;

    private readonly Lock _lock = new();
    private readonly (long Bytes, DateTimeOffset Timestamp)[] _buffer = new (long, DateTimeOffset)[BufferSize];
    private int _bufferIndex;
    private int _bufferCount;

    private bool _isDownloading;
    private string? _programName;
    private string? _episodeTitle;
    private string? _seasonEpisodeLabel;
    private long _bytesDownloaded;
    private long? _totalSize;
    private double _totalDurationSeconds;
    private DateTimeOffset? _lastDownloadedAt;
    private int _completedLast7Days;
    private int _failedCount;

    public bool IsDownloading
    {
        get { lock (_lock) { return _isDownloading; } }
    }

    public string? ProgramName
    {
        get { lock (_lock) { return _programName; } }
    }

    public string? EpisodeTitle
    {
        get { lock (_lock) { return _episodeTitle; } }
    }

    public string? SeasonEpisodeLabel
    {
        get { lock (_lock) { return _seasonEpisodeLabel; } }
    }

    public long BytesDownloaded
    {
        get { lock (_lock) { return _bytesDownloaded; } }
    }

    public long? TotalSize
    {
        get { lock (_lock) { return _totalSize; } }
    }

    public DateTimeOffset? LastDownloadedAt
    {
        get { lock (_lock) { return _lastDownloadedAt; } }
    }

    public int CompletedLast7Days
    {
        get { lock (_lock) { return _completedLast7Days; } }
    }

    public int FailedCount
    {
        get { lock (_lock) { return _failedCount; } }
    }

    public double? RateBytesPerSecond
    {
        get
        {
            lock (_lock)
            {
                return CalculateRate();
            }
        }
    }

    public TimeSpan? EstimatedRemaining
    {
        get
        {
            lock (_lock)
            {
                if (_totalSize is null)
                {
                    return null;
                }

                double? rate = CalculateRate();
                if (rate is null or <= 0)
                {
                    return null;
                }

                long remaining = _totalSize.Value - _bytesDownloaded;
                if (remaining <= 0)
                {
                    return TimeSpan.Zero;
                }

                return TimeSpan.FromSeconds(remaining / rate.Value);
            }
        }
    }

    public void StartDownload(
        string programName, string episodeTitle, string? seasonEpisodeLabel,
        long? totalSize, double? totalDurationSeconds = null)
    {
        lock (_lock)
        {
            _isDownloading = true;
            _programName = programName;
            _episodeTitle = episodeTitle;
            _seasonEpisodeLabel = seasonEpisodeLabel;
            _totalSize = totalSize;
            _totalDurationSeconds = totalDurationSeconds ?? 0;
            _bytesDownloaded = 0;
            _bufferIndex = 0;
            _bufferCount = 0;
        }

        broadcaster.Publish(new DownloadProgressUpdatedEvent());
    }

    public void ReportProgress(FfmpegProgressData data)
    {
        lock (_lock)
        {
            _bytesDownloaded = data.BytesDownloaded;

            if (_totalDurationSeconds > 0 && data.Time.TotalSeconds > 1.0)
            {
                _totalSize = (long)(data.BytesDownloaded * (_totalDurationSeconds / data.Time.TotalSeconds));
            }
            else if (_totalSize is null && data.EstimatedTotalBytes is not null)
            {
                _totalSize = data.EstimatedTotalBytes;
            }

            _buffer[_bufferIndex] = (data.BytesDownloaded, timeProvider.GetUtcNow());
            _bufferIndex = (_bufferIndex + 1) % BufferSize;
            if (_bufferCount < BufferSize)
            {
                _bufferCount++;
            }
        }

        broadcaster.Publish(new DownloadProgressUpdatedEvent());
    }

    public void CompleteDownload()
    {
        lock (_lock)
        {
            _isDownloading = false;
            _lastDownloadedAt = timeProvider.GetUtcNow();
            _completedLast7Days++;
            _programName = null;
            _episodeTitle = null;
            _seasonEpisodeLabel = null;
            _bytesDownloaded = 0;
            _totalSize = null;
            _bufferIndex = 0;
            _bufferCount = 0;
        }

        broadcaster.Publish(new DownloadProgressUpdatedEvent());
    }

    public void FailDownload()
    {
        lock (_lock)
        {
            _isDownloading = false;
            _failedCount++;
            _programName = null;
            _episodeTitle = null;
            _seasonEpisodeLabel = null;
            _bytesDownloaded = 0;
            _totalSize = null;
            _bufferIndex = 0;
            _bufferCount = 0;
        }

        broadcaster.Publish(new DownloadProgressUpdatedEvent());
    }

    public void SetStatistics(DateTimeOffset? lastDownloadedAt, int completedLast7Days, int failedCount)
    {
        lock (_lock)
        {
            _lastDownloadedAt = lastDownloadedAt;
            _completedLast7Days = completedLast7Days;
            _failedCount = failedCount;
        }
    }

    private double? CalculateRate()
    {
        if (_bufferCount < 2)
        {
            return null;
        }

        int oldestIndex = _bufferCount < BufferSize
            ? 0
            : _bufferIndex;

        int newestIndex = (_bufferIndex - 1 + BufferSize) % BufferSize;

        (long Bytes, DateTimeOffset Timestamp) oldest = _buffer[oldestIndex];
        (long Bytes, DateTimeOffset Timestamp) newest = _buffer[newestIndex];

        double elapsed = (newest.Timestamp - oldest.Timestamp).TotalSeconds;
        if (elapsed <= 0)
        {
            return null;
        }

        return (newest.Bytes - oldest.Bytes) / elapsed;
    }
}
