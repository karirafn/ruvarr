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
        return JsonSerializer.Deserialize<RuvarrSettings>(content) ?? RuvarrSettings.Empty;
    }
}
