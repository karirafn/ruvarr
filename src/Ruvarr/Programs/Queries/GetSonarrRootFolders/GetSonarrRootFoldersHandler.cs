using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Queries.GetSonarrRootFolders;

internal sealed class GetSonarrRootFoldersHandler(ISonarrClient sonarrClient)
    : IRequestHandler<GetSonarrRootFoldersQuery, IReadOnlyList<RootFolder>>
{
    public async Task<IReadOnlyList<RootFolder>> Handle(GetSonarrRootFoldersQuery request, CancellationToken cancellationToken) =>
        await sonarrClient.GetRootFoldersAsync(cancellationToken);
}
