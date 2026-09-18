using System.Net;

namespace Ruvarr.UnitTests.Infrastructure.Resilience;

/// <summary>
/// A scripted <see cref="HttpMessageHandler"/> for resilience pipeline testing.
/// Each <see cref="SendAsync"/> dequeues the next response spec; the last spec repeats
/// when the queue drains. Supports optional per-response delay and <c>Retry-After</c> header.
/// </summary>
internal sealed class ScriptedHttpMessageHandler : HttpMessageHandler
{
    private readonly ResponseSpec[] _script;
    private int _index;

    public int RequestCount { get; private set; }
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
        RequestCount++;
        CapturedRequests.Add(request);

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
