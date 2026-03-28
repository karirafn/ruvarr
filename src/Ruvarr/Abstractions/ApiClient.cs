namespace Ruvarr.Abstractions;

internal abstract class ApiClient(ILogger logger, HttpClient httpClient)
{
    protected async Task<TResponse?> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage message = await httpClient.GetAsync(path, cancellationToken);

            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("GET {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, Truncate(content));
                return default;
            }

            TResponse? response = await message.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

            return response;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "GET {Path} failed: Readon: {Message}", path, ex.Message);
            return default;
        }
    }

    protected async Task<IReadOnlyList<TResponse>> GetMany<TResponse>(string path, CancellationToken cancellationToken) =>
        await GetAsync<IReadOnlyList<TResponse>>(path, cancellationToken) ?? [];

    protected async Task PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage message = await httpClient.PostAsJsonAsync(path, body, cancellationToken);

            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("POST {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, Truncate(content));
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "POST {Path} failed: Reason: {Message}", path, ex.Message);
        }
    }

    protected async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage message = await httpClient.PostAsJsonAsync(path, body, cancellationToken);

            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("POST {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, Truncate(content));
                return default;
            }

            TResponse? response = await message.Content.ReadFromJsonAsync<TResponse>(cancellationToken);

            return response;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "POST {Path} failed: Reason: {Message}", path, ex.Message);
            return default;
        }
    }

    private const int MaxLogContentLength = 512;

    private static string Truncate(string value) =>
        value.Length > MaxLogContentLength ? value[..MaxLogContentLength] : value;
}