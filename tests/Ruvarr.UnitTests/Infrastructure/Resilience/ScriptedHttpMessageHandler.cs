using System.Net;

namespace Ruvarr.UnitTests.Infrastructure.Resilience;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> for resilience pipeline testing.
/// Each <see cref="SendAsync"/> dequeues the next response spec; the last spec repeats
/// when the queue drains. Supports optional per-response delay and <c>Retry-After</c> header.
/// </summary>
/// <remarks>
/// The resilience pipeline dispatches retries sequentially (not concurrently), so
/// <c>_index</c> advances without a race. <c>RequestCount</c> uses <c>Interlocked</c>
/// for safety should the handler ever be used under concurrent dispatch.
/// </remarks>
internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly ResponseSpec[] _script;
    private int _index;
    private int _requestCount;

    public int RequestCount => _requestCount;
    public List<HttpRequestMessage> CapturedRequests { get; } = [];

    public ScriptedHttpMessageHandler(params ResponseSpec[] script)
    {
        ArgumentOutOfRangeException.ThrowIfZero(script.Length);
        _script = script;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        CapturedRequests.Add(request);

        // _index is advanced sequentially under the retry contract (one attempt at a time).
        ResponseSpec spec = _index < _script.Length ? _script[_index++] : _script[^1];

        if (spec.Delay.HasValue)
        {
            await Task.Delay(spec.Delay.Value, cancellationToken);
        }

        HttpResponseMessage response = new(spec.Status);

        if (spec.RetryAfter.HasValue)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(spec.RetryAfter.Value);
        }

        return response;
    }
}

/// <summary>Response specification for <see cref="ScriptedHttpMessageHandler"/>.</summary>
internal sealed record ResponseSpec(
    HttpStatusCode Status,
    TimeSpan? Delay = null,
    TimeSpan? RetryAfter = null);
