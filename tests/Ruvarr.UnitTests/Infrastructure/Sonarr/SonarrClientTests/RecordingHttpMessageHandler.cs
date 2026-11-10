using System.Net;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr.SonarrClientTests;

internal sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody = "{}") : HttpMessageHandler
{
    public HttpMethod? Method { get; private set; }

    public string? Path { get; private set; }

    public string? RequestBody { get; private set; }

    public Exception? ThrowOnSend { get; init; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Method = request.Method;
        Path = request.RequestUri?.PathAndQuery;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        return new HttpResponseMessage(statusCode) { Content = new StringContent(responseBody) };
    }
}
