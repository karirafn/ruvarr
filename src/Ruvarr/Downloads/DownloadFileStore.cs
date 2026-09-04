using Ruvarr.Settings;

namespace Ruvarr.Downloads;

internal sealed class DownloadFileStore(ILogger<DownloadFileStore> logger)
{
    public static string IncompletePath(RuvarrSettings settings, string fileName) =>
        Path.Join(settings.ResolvedIncompleteDirectory, fileName);

    public static string CompletedPath(RuvarrSettings settings, string fileName) =>
        Path.Join(settings.ResolvedEpisodeDownloadDirectory, fileName);

    public static void EnsureIncompleteDirectory(RuvarrSettings settings) =>
        Directory.CreateDirectory(settings.ResolvedIncompleteDirectory);

    public static bool CompletedFileExists(RuvarrSettings settings, string fileName) =>
        File.Exists(CompletedPath(settings, fileName));

    public static string MoveToCompleted(RuvarrSettings settings, string fileName)
    {
        string incompletePath = IncompletePath(settings, fileName);
        string completedPath = CompletedPath(settings, fileName);

        Directory.CreateDirectory(settings.ResolvedEpisodeDownloadDirectory);
        File.Move(incompletePath, completedPath, overwrite: true);

        return completedPath;
    }

    public void DeleteIncomplete(RuvarrSettings settings, string fileName)
    {
        string filePath = IncompletePath(settings, fileName);
        string siblingPath = Path.ChangeExtension(filePath, ".tmp" + Path.GetExtension(filePath));

        TryDelete(filePath);
        TryDelete(siblingPath);
    }

    private void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to delete file {Path}", path);
        }
    }
}
