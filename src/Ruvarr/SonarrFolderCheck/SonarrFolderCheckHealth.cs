namespace Ruvarr.SonarrFolderCheck;

// In-memory like SonarrWebhookHealth: the check runs at startup, so a restart loses nothing that matters.
internal sealed class SonarrFolderCheckHealth
{
    private readonly Lock _lock = new();
    private SonarrFolderCheckResult? _lastResult;

    // Null until the first check has run, which reads as Unknown.
    public SonarrFolderCheckResult? LastResult
    {
        get { lock (_lock) { return _lastResult; } }
    }

    public SonarrFolderCheckStatus Status => LastResult?.Status ?? SonarrFolderCheckStatus.Unknown;

    public void Record(SonarrFolderCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        lock (_lock)
        {
            _lastResult = result;
        }
    }
}
