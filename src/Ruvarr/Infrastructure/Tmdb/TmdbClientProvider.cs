using Ruvarr.Settings;

using TMDbLib.Client;

namespace Ruvarr.Infrastructure.Tmdb;

internal sealed class TmdbClientProvider(ISettingsStore settingsStore) : IDisposable
{
    private static readonly NfcTmdbSerializer NfcSerializer = new();

    private readonly Lock _lock = new();

    private string _currentApiKey = string.Empty;
    private TMDbClient _client = new("unconfigured", serializer: NfcSerializer);

    public TMDbClient Client
    {
        get
        {
            string apiKey = settingsStore.Current.TmdbApiKey;
            string effectiveKey = string.IsNullOrWhiteSpace(apiKey) ? "unconfigured" : apiKey;

            if (effectiveKey == _currentApiKey)
            {
                return _client;
            }

            lock (_lock)
            {
                if (effectiveKey == _currentApiKey)
                {
                    return _client;
                }

                _client.Dispose();
                _client = new TMDbClient(effectiveKey, serializer: NfcSerializer);
                _currentApiKey = effectiveKey;

                return _client;
            }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
    }
}
