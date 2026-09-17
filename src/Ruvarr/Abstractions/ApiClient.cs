namespace Ruvarr.Abstractions;

internal abstract class ApiClient(ILogger logger, HttpClient httpClient)
{
    protected async Task<Result<TResponse>> GetAsync<TResponse>(string path, CancellationToken cancellationToken)
        where TResponse : notnull
    {
        try
        {
            HttpResponseMessage message = await httpClient.GetAsync(path, cancellationToken);

            if (message.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                string notFoundContent = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("GET {Path} returned 404. Reason: {Content}", path, SanitizeAndTruncate(notFoundContent));
                return ApiClientErrors.NotFound;
            }

            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("GET {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, SanitizeAndTruncate(content));
                return ApiClientErrors.RequestFailed;
            }

            TResponse? response = await message.Content.ReadFromRuvarrJsonAsync<TResponse>(cancellationToken);

            if (response is null)
            {
                logger.LogWarning("GET {Path} returned 200 but deserialized to null.", path);
                return ApiClientErrors.RequestFailed;
            }

            return response;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout throws TaskCanceledException (derives from OperationCanceledException)
            // even when the caller's token is not cancelled — treat as a request failure, not cancellation.
            logger.LogError(ex, "GET {Path} timed out.", path);
            return ApiClientErrors.RequestFailed;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "GET {Path} failed: Reason: {Message}", path, SanitizeAndTruncate(ex.Message));
            return ApiClientErrors.RequestFailed;
        }
    }

    protected async Task<Result<IReadOnlyList<TResponse>>> GetMany<TResponse>(
        string path,
        CancellationToken cancellationToken)
        where TResponse : notnull
    {
        try
        {
            HttpResponseMessage message = await httpClient.GetAsync(path, cancellationToken);

            // Intentional asymmetry with GetAsync: all non-2xx responses (including 404) map to
            // RequestFailed here. List endpoints return an empty body, not 404, so no caller
            // needs the NotFound distinction.
            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("GET {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, SanitizeAndTruncate(content));
                return ApiClientErrors.RequestFailed;
            }

            IReadOnlyList<TResponse>? response = await message.Content.ReadFromRuvarrJsonAsync<IReadOnlyList<TResponse>>(cancellationToken);

            // A 200 with a null or empty deserialized body is a legitimate empty collection.
            IReadOnlyList<TResponse> items = response ?? [];
            return new Result<IReadOnlyList<TResponse>>(items);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout throws TaskCanceledException (derives from OperationCanceledException)
            // even when the caller's token is not cancelled — treat as a request failure, not cancellation.
            logger.LogError(ex, "GET {Path} timed out.", path);
            return ApiClientErrors.RequestFailed;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "GET {Path} failed: Reason: {Message}", path, SanitizeAndTruncate(ex.Message));
            return ApiClientErrors.RequestFailed;
        }
    }

    protected async Task PostAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        try
        {
            HttpResponseMessage message = await httpClient.PostAsJsonAsync(path, body, cancellationToken);

            if (!message.IsSuccessStatusCode)
            {
                string content = await message.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("POST {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, SanitizeAndTruncate(content));
            }
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout throws TaskCanceledException (derives from OperationCanceledException)
            // even when the caller's token is not cancelled — treat as a request failure, not cancellation.
            logger.LogWarning(ex, "POST {Path} timed out.", path);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "POST {Path} failed: Reason: {Message}", path, SanitizeAndTruncate(ex.Message));
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
                logger.LogWarning("POST {Path} returned status code {Code}. Reason: {Content}", path, message.StatusCode, SanitizeAndTruncate(content));
                return default;
            }

            TResponse? response = await message.Content.ReadFromRuvarrJsonAsync<TResponse>(cancellationToken);

            return response;
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's own timeout throws TaskCanceledException (derives from OperationCanceledException)
            // even when the caller's token is not cancelled — treat as a request failure, not cancellation.
            logger.LogWarning(ex, "POST {Path} timed out.", path);
            return default;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "POST {Path} failed: Reason: {Message}", path, SanitizeAndTruncate(ex.Message));
            return default;
        }
    }

    private const int MaxLogContentLength = 512;

    private static string SanitizeAndTruncate(string value)
    {
        string sanitized = value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
        return sanitized.Length > MaxLogContentLength ? sanitized[..MaxLogContentLength] : sanitized;
    }
}
