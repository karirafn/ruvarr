using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Commands.AddProgramToSonarr;

internal sealed class AddProgramToSonarrHandler(
    RuvarrDbContext dbContext,
    ISonarrClient sonarrClient) : IRequestHandler<AddProgramToSonarrCommand>
{
    public async Task<RuvarrResult> Handle(AddProgramToSonarrCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        RuvProgram? program = await dbContext.Set<RuvProgram>()
            .Include(x => x.Series)
            .Where(x => x.RuvId == command.RuvId)
            .FirstOrDefaultAsync(cancellationToken);

        if (program is null)
        {
            return ProgramErrors.ProgramNotFound;
        }

        if (program.Series is null)
        {
            return ProgramErrors.ProgramNotMatched;
        }

        IReadOnlyList<RootFolder> rootFolders = await sonarrClient.GetRootFoldersAsync(cancellationToken);
        if (!rootFolders.Any(f => f.Path == command.RootFolderPath))
        {
            return SonarrErrors.InvalidRootFolderPath;
        }

        IReadOnlyList<QualityProfile> qualityProfiles = await sonarrClient.GetQualityProfilesAsync(cancellationToken);
        if (!qualityProfiles.Any(p => p.Id == command.QualityProfileId))
        {
            return SonarrErrors.InvalidQualityProfileId;
        }

        AddSeriesRequest request = new(
            program.Series.TvdbId,
            program.Name,
            command.QualityProfileId,
            command.RootFolderPath,
            Monitored: true,
            SeasonFolder: true,
            new AddSeriesOptions(SearchForMissingEpisodes: false));

        Series? result = await sonarrClient.AddSeriesAsync(request, cancellationToken);

        if (result is null)
        {
            return SonarrErrors.AddSeriesFailed;
        }

        return RuvarrResult.Success;
    }
}
