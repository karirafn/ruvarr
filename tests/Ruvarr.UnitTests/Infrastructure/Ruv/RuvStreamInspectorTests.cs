using System.Diagnostics.CodeAnalysis;
using System.Net;

using Ruvarr.Infrastructure.Ruv;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv;

public sealed class RuvStreamInspectorTests
{
    private const long SegmentContentLength = 500_000;

    private static readonly Uri PlaylistUri = new("http://ruv.is/stream/index.m3u8");

    [Fact]
    public async Task ReturnsCorrectEstimate_WhenPlaylistHasMultipleSegments()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXT-X-TARGETDURATION:10
            #EXTINF:10.0,
            segment001.ts
            #EXTINF:10.0,
            segment002.ts
            #EXTINF:10.0,
            segment003.ts
            #EXT-X-ENDLIST
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(m3u8, SegmentContentLength);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBe(3 * SegmentContentLength);
    }

    [Fact]
    public async Task ReturnsNull_WhenPlaylistIsMaster()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=800000
            low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=1400000
            high/index.m3u8
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(m3u8, SegmentContentLength);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenPlaylistHasOnlyCommentsAndEmptyLines()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXT-X-TARGETDURATION:10

            #EXT-X-ENDLIST
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(m3u8, SegmentContentLength);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenHeadResponseMissingContentLength()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXTINF:10.0,
            segment001.ts
            #EXT-X-ENDLIST
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(m3u8, contentLength: null);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenGetFails()
    {
        // Arrange
        using HttpResponseMessage getResponse = new(HttpStatusCode.InternalServerError);
        using HttpResponseMessage headResponse = new(HttpStatusCode.OK);
        using StubHandler handler = new(getResponse, headResponse);
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenHeadThrows()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXTINF:10.0,
            segment001.ts
            #EXT-X-ENDLIST
            """;

        using HttpResponseMessage getResponse = new(HttpStatusCode.OK) { Content = new StringContent(m3u8) };
        using StubHandler handler = new(getResponse, headResponse: null);
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ResolvesRelativeSegmentUrls()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXTINF:10.0,
            ../segments/segment001.ts
            #EXTINF:10.0,
            ../segments/segment002.ts
            #EXT-X-ENDLIST
            """;

        Uri playlistUri = new("http://ruv.is/stream/hls/index.m3u8");
        Uri? capturedHeadUri = null;

        using HttpResponseMessage getResponse = new(HttpStatusCode.OK) { Content = new StringContent(m3u8) };
        using HttpResponseMessage headResponse = CreateHeadResponse(SegmentContentLength);
        using StubHandler handler = new(getResponse, headResponse, onHead: uri => capturedHeadUri = uri);
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(playlistUri, CancellationToken.None);

        // Assert
        result.ShouldBe(2 * SegmentContentLength);
        capturedHeadUri.ShouldNotBeNull();
        capturedHeadUri.AbsolutePath.ShouldBe("/stream/segments/segment001.ts");
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "Disposables returned to caller via CompositeDisposable")]
    private static (RuvStreamInspector Sut, IDisposable Disposables) CreateInspector(
        string m3u8Content, long? contentLength)
    {
        HttpResponseMessage getResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(m3u8Content)
        };

        HttpResponseMessage headResponse = CreateHeadResponse(contentLength);
        StubHandler handler = new(getResponse, headResponse);
        HttpClient httpClient = new(handler);

        RuvStreamInspector sut = new(httpClient);
        CompositeDisposable disposables = new(getResponse, headResponse, handler, httpClient);

        return (sut, disposables);
    }

    private static HttpResponseMessage CreateHeadResponse(long? contentLength)
    {
        HttpResponseMessage response = new(HttpStatusCode.OK);
        if (contentLength is not null)
        {
            response.Content = new StringContent("");
            response.Content.Headers.ContentLength = contentLength;
        }

        return response;
    }

    private sealed class CompositeDisposable(params IDisposable[] disposables) : IDisposable
    {
        public void Dispose()
        {
            foreach (IDisposable disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }

    private sealed class StubHandler(
        HttpResponseMessage getResponse,
        HttpResponseMessage? headResponse,
        Action<Uri>? onHead = null) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                return Task.FromResult(getResponse);
            }

            if (request.Method == HttpMethod.Head)
            {
                onHead?.Invoke(request.RequestUri!);
                return headResponse is not null
                    ? Task.FromResult(headResponse)
                    : throw new HttpRequestException("HEAD failed");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
