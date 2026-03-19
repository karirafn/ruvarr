using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Queries.GetSonarrQualityProfiles;

internal sealed class GetSonarrQualityProfilesHandler(ISonarrClient sonarrClient)
    : IRequestHandler<GetSonarrQualityProfilesQuery, IReadOnlyList<QualityProfile>>
{
    public async Task<IReadOnlyList<QualityProfile>> Handle(GetSonarrQualityProfilesQuery request, CancellationToken cancellationToken) =>
        await sonarrClient.GetQualityProfilesAsync(cancellationToken);
}
