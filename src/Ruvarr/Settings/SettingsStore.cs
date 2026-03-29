using System.Text.Json;

namespace Ruvarr.Settings;

internal sealed class SettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile RuvarrSettings _current;

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
        _current = LoadFromFile(filePath);
    }

    public RuvarrSettings Current => _current;

    public event Action? SettingsChanged;

    public async Task SaveAsync(RuvarrSettings settings, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);

        try
        {
            string? directory = Path.GetDirectoryName(_filePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = _filePath + ".tmp";
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken);
            }

            File.Move(tempPath, _filePath, overwrite: true);
            _current = settings;
            SettingsChanged?.Invoke();
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }

    private static RuvarrSettings LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory is not null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(RuvarrSettings.Empty, SerializerOptions);
            File.WriteAllText(filePath, json);

            return RuvarrSettings.Empty;
        }

        string content = File.ReadAllText(filePath);
        RuvarrSettings settings = JsonSerializer.Deserialize<RuvarrSettings>(content) ?? RuvarrSettings.Empty;

        return MigrateAbsolutePaths(settings);
    }

    private static RuvarrSettings MigrateAbsolutePaths(RuvarrSettings settings)
    {
        string prefix = settings.DownloadsRoot + "/";

        string episodeDir = settings.EpisodeDownloadDirectory;
        if (episodeDir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            episodeDir = episodeDir[prefix.Length..];
        }

        string movieDir = settings.MovieDownloadDirectory;
        if (movieDir.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            movieDir = movieDir[prefix.Length..];
        }

        if (episodeDir == settings.EpisodeDownloadDirectory && movieDir == settings.MovieDownloadDirectory)
        {
            return settings;
        }

        return settings with
        {
            EpisodeDownloadDirectory = episodeDir,
            MovieDownloadDirectory = movieDir
        };
    }
}
