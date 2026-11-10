using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Queries.GetSonarrQualityProfiles;

internal sealed class GetSonarrQualityProfilesHandler(ISonarrClient sonarrClient)
    : IRequestHandler<GetSonarrQualityProfilesQuery, Result<IReadOnlyList<QualityProfile>>>
{
    public Task<Result<IReadOnlyList<QualityProfile>>> Handle(GetSonarrQualityProfilesQuery request, CancellationToken cancellationToken) =>
        sonarrClient.GetQualityProfilesAsync(cancellationToken);
}
