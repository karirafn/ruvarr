using System.Diagnostics.CodeAnalysis;
using System.Net;

using Ruvarr.Infrastructure.Ruv;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv;

public sealed class RuvStreamInspectorTests
{
    private const long SegmentContentLength = 500_000;

    private static readonly Uri PlaylistUri = new("https://ruv.is/stream/index.m3u8");

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
    public async Task PicksHighestBandwidthVariant_WhenPlaylistIsMaster()
    {
        // Arrange
        string masterPlaylist = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=3600000,CODECS="avc1",RESOLUTION=1920x1080
            high/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=800000,CODECS="avc1",RESOLUTION=640x360
            low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=1400000,CODECS="avc1",RESOLUTION=1280x720
            mid/index.m3u8
            """;

        string mediaPlaylist = """
            #EXTM3U
            #EXT-X-TARGETDURATION:10
            #EXTINF:10.0,
            segment001.ts
            #EXTINF:10.0,
            segment002.ts
            #EXTINF:10.0,
            segment003.ts
            #EXTINF:10.0,
            segment004.ts
            #EXT-X-ENDLIST
            """;

        Dictionary<string, HttpResponseMessage> getResponses = new()
        {
            [PlaylistUri.AbsoluteUri] = new(HttpStatusCode.OK) { Content = new StringContent(masterPlaylist) },
            ["https://ruv.is/stream/high/index.m3u8"] = new(HttpStatusCode.OK) { Content = new StringContent(mediaPlaylist) }
        };

        using MultiGetStubHandler handler = new(getResponses, _ => CreateHeadResponse(SegmentContentLength));
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBe(4 * SegmentContentLength);
    }

    [Fact]
    public async Task ReturnsNull_WhenMasterVariantPointsToDifferentHost()
    {
        // Arrange
        string masterPlaylist = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=800000
            https://evil.com/low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=1400000
            https://evil.com/high/index.m3u8
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(masterPlaylist, SegmentContentLength);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ReturnsNull_WhenMasterVariantFetchFails()
    {
        // Arrange
        string masterPlaylist = """
            #EXTM3U
            #EXT-X-STREAM-INF:BANDWIDTH=800000
            low/index.m3u8
            #EXT-X-STREAM-INF:BANDWIDTH=1400000
            high/index.m3u8
            """;

        Dictionary<string, HttpResponseMessage> getResponses = new()
        {
            [PlaylistUri.AbsoluteUri] = new(HttpStatusCode.OK) { Content = new StringContent(masterPlaylist) },
            ["https://ruv.is/stream/high/index.m3u8"] = new(HttpStatusCode.InternalServerError)
        };

        using MultiGetStubHandler handler = new(getResponses, _ => CreateHeadResponse(SegmentContentLength));
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

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
        using StubHandler handler = new(getResponse, headResponseFactory: () => new(HttpStatusCode.OK));
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
        using StubHandler handler = new(getResponse, headResponseFactory: null);
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

        Uri playlistUri = new("https://ruv.is/stream/hls/index.m3u8");
        List<Uri> capturedHeadUris = [];

        using HttpResponseMessage getResponse = new(HttpStatusCode.OK) { Content = new StringContent(m3u8) };
        using StubHandler handler = new(getResponse,
            headResponseFactory: () => CreateHeadResponse(SegmentContentLength),
            onHead: uri => capturedHeadUris.Add(uri));
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(playlistUri, CancellationToken.None);

        // Assert
        result.ShouldBe(2 * SegmentContentLength);
        capturedHeadUris.Count.ShouldBe(2);
        capturedHeadUris[0].AbsolutePath.ShouldBe("/stream/segments/segment001.ts");
        capturedHeadUris[1].AbsolutePath.ShouldBe("/stream/segments/segment002.ts");
    }

    [Fact]
    public async Task AveragesMultipleSegmentSizes_WhenSegmentsHaveDifferentSizes()
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
            #EXTINF:10.0,
            segment004.ts
            #EXTINF:10.0,
            segment005.ts
            #EXT-X-ENDLIST
            """;

        long[] segmentSizes = [400_000, 600_000, 500_000];
        int headCallIndex = 0;

        using HttpResponseMessage getResponse = new(HttpStatusCode.OK) { Content = new StringContent(m3u8) };
        using StubHandler handler = new(getResponse, headResponseFactory: () =>
        {
            int index = headCallIndex++;
            return index < segmentSizes.Length
                ? CreateHeadResponse(segmentSizes[index])
                : CreateHeadResponse(SegmentContentLength);
        });
        using HttpClient httpClient = new(handler);
        RuvStreamInspector sut = new(httpClient);

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        long expectedAverage = (long)segmentSizes.Average();
        result.ShouldBe(5 * expectedAverage);
    }

    [Fact]
    public async Task ReturnsNull_WhenSegmentPointsToDifferentHost()
    {
        // Arrange
        string m3u8 = """
            #EXTM3U
            #EXTINF:10.0,
            http://169.254.169.254/latest/meta-data
            #EXT-X-ENDLIST
            """;

        (RuvStreamInspector sut, IDisposable disposables) = CreateInspector(m3u8, SegmentContentLength);
        using IDisposable _ = disposables;

        // Act
        long? result = await sut.EstimateStreamSizeAsync(PlaylistUri, CancellationToken.None);

        // Assert
        result.ShouldBeNull();
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "Disposables returned to caller via CompositeDisposable")]
    private static (RuvStreamInspector Sut, IDisposable Disposables) CreateInspector(
        string m3u8Content, long? contentLength)
    {
        HttpResponseMessage getResponse = new(HttpStatusCode.OK)
        {
            Content = new StringContent(m3u8Content)
        };

        StubHandler handler = new(getResponse, headResponseFactory: () => CreateHeadResponse(contentLength));
        HttpClient httpClient = new(handler);

        RuvStreamInspector sut = new(httpClient);
        CompositeDisposable disposables = new(getResponse, handler, httpClient);

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
        Func<HttpResponseMessage>? headResponseFactory = null,
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
                return headResponseFactory is not null
                    ? Task.FromResult(headResponseFactory())
                    : throw new HttpRequestException("HEAD failed");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class MultiGetStubHandler(
        Dictionary<string, HttpResponseMessage> getResponses,
        Func<Uri, HttpResponseMessage> headResponseFactory,
        Action<Uri>? onHead = null) : DelegatingHandler
    {
        private static readonly HttpResponseMessage NotFoundResponse = new(HttpStatusCode.NotFound);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get)
            {
                string uri = request.RequestUri!.AbsoluteUri;
                return getResponses.TryGetValue(uri, out HttpResponseMessage? response)
                    ? Task.FromResult(response)
                    : Task.FromResult(NotFoundResponse);
            }

            if (request.Method == HttpMethod.Head)
            {
                onHead?.Invoke(request.RequestUri!);
                return Task.FromResult(headResponseFactory(request.RequestUri!));
            }

            return Task.FromResult(NotFoundResponse);
        }
    }
}
