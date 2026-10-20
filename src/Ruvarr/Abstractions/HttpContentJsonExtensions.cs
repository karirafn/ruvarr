using System.Text.Json;

namespace Ruvarr.Abstractions;

internal static class HttpContentJsonExtensions
{
    internal static Task<T?> ReadFromRuvarrJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken) =>
        content.ReadFromJsonAsync<T>(RuvarrJson.Default, cancellationToken);

    internal static Task<T?> ReadFromRuvarrJsonAsync<T>(this HttpContent content, JsonSerializerOptions options, CancellationToken cancellationToken) =>
        content.ReadFromJsonAsync<T>(options, cancellationToken);
}
