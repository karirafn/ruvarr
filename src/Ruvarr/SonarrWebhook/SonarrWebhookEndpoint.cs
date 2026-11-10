using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

using Ruvarr.Abstractions;
using Ruvarr.SonarrWebhook.Commands.ReceiveSonarrWebhook;

namespace Ruvarr.SonarrWebhook;

internal static class SonarrWebhookEndpoint
{
    internal const string Path = "/api/sonarr/webhook";

    // Unauthenticated by decision: Ruvarr and Sonarr share a private network, and a delivery can
    // only queue a re-sync of existing programs (docs/adr/0014).
    internal static IEndpointRouteBuilder MapSonarrWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, ReceiveAsync).AllowAnonymous();

        return endpoints;
    }

    // Sonarr refuses to save a webhook whose test delivery fails, so every well-formed delivery is
    // acknowledged, including ones Ruvarr ignores.
    private static async Task<IResult> ReceiveAsync(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReceiveSonarrWebhookCommand? delivery,
        IRequestHandler<ReceiveSonarrWebhookCommand> handler,
        CancellationToken cancellationToken)
    {
        if (delivery is not null)
        {
            await handler.Handle(delivery, cancellationToken);
        }

        return Results.Ok();
    }
}
