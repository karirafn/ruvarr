// Tests the resilience pipeline wired into the DI-built IRuvClient by calling the
// real AddRuv() registration and substituting the primary HTTP handler via
// ConfigurePrimaryHttpMessageHandler. No Docker or database is required.

using System.Net;

using Microsoft.Extensions.Http.Resilience;

using Ruvarr.Infrastructure.Ruv;
using Ruvarr.UnitTests.Infrastructure.Resilience;

using Shouldly;

namespace Ruvarr.UnitTests.Infrastructure.Ruv;

public sealed class RuvResilienceTests
{
    [Fact]
    public async Task WhenTransient503ThenSuccess_ClientSucceedsAfterRetry()
    {
        // Arrange — script [503, 200] through the primary handler so the resilience
        // pipeline retries the first failure and returns the success on the second attempt.
        using ScriptedHttpMessageHandler handler = new(
            new ResponseSpec(HttpStatusCode.ServiceUnavailable),
            new ResponseSpec(HttpStatusCode.OK));

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ruv:BaseAddress"] = "https://api.ruv.is/",
            })
            .Build();

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddRuv();

        // Post-configure the named client's options to sub-second delays so the
        // test is fast while keeping Total >= Attempt and SamplingDuration >= 2*Attempt.
        // AddStandardResilienceHandler registers options under "{clientName}-standard".
        string clientName = nameof(IRuvClient);
        services.Configure<HttpStandardResilienceOptions>($"{clientName}-standard", options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.Delay = TimeSpan.FromMilliseconds(10);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
        });

        // Substitute the primary handler after AddRuv so it is innermost.
        services.AddHttpClient<IRuvClient, RuvClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using ServiceProvider provider = services.BuildServiceProvider();
        IHttpClientFactory factory = provider.GetRequiredService<IHttpClientFactory>();
        using HttpClient client = factory.CreateClient(clientName);

        // Act
        using HttpResponseMessage response = await client.GetAsync(
            "https://api.ruv.is/api/programs/program/1234/all",
            CancellationToken.None);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        handler.RequestCount.ShouldBe(2);
    }
}
