using System.Net.Http.Json;

using Microsoft.Extensions.Logging;

namespace Ruvarr.Abstractions;

internal abstract class ApiClient(ILogger logger, HttpClient httpClient)
{
    protected async Task<TResponse?> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        HttpResponseMessage message = await httpClient.GetAsync(path, cancellationToken);

        if (!message.IsSuccessStatusCode)
        {
            string content = await message.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("GET {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, content);
            return default;
        }

        TResponse? response = await message.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

        return response;
    }

    protected async Task<IReadOnlyList<TResponse>> GetMany<TResponse>(string path, CancellationToken cancellationToken) =>
        await GetAsync<IReadOnlyList<TResponse>>(path, cancellationToken) ?? [];

    protected async Task PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        HttpResponseMessage message = await httpClient.PostAsJsonAsync(path, body, cancellationToken);

        if (!message.IsSuccessStatusCode)
        {
            string content = await message.Content.ReadAsStringAsync(cancellationToken);
            logger.LogWarning("POST {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, content);
        }
    }
}