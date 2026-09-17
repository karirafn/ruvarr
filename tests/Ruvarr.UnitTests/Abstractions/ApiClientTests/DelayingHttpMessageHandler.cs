namespace Ruvarr.UnitTests.Abstractions.ApiClientTests;

/// <summary>
/// A test <see cref="HttpMessageHandler"/> that delays its response past the client's configured
/// <see cref="HttpClient.Timeout"/>, causing <see cref="HttpClient"/> to throw
/// <see cref="TaskCanceledException"/> from its own timeout (not from caller cancellation).
/// </summary>
internal sealed class DelayingHttpMessageHandler(TimeSpan delay) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(delay, cancellationToken);
        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}
