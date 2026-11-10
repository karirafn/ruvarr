using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Queries.GetSonarrRootFolders;

internal sealed class GetSonarrRootFoldersHandler(ISonarrClient sonarrClient)
    : IRequestHandler<GetSonarrRootFoldersQuery, Result<IReadOnlyList<RootFolder>>>
{
    public Task<Result<IReadOnlyList<RootFolder>>> Handle(GetSonarrRootFoldersQuery request, CancellationToken cancellationToken) =>
        sonarrClient.GetRootFoldersAsync(cancellationToken);
}
