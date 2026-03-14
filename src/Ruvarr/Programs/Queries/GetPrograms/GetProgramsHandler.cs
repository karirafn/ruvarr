using System.Runtime.CompilerServices;

using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Programs.Queries.GetPrograms;

internal sealed class GetProgramsHandler(RuvarrDbContext dbContext) : IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>
{
    public async IAsyncEnumerable<ProgramSummary> Handle(GetProgramsQuery request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IQueryable<RuvProgram> query = dbContext
            .Set<RuvProgram>()
            .Where(x => x.HasMultipleEpisodes);

        if (!string.IsNullOrWhiteSpace(request.Channel))
        {
            query = query.Where(x => x.Channel == request.Channel);
        }

        if (request.IsProgramMonitored is not null)
        {
            query = query.Where(x => x.IsMonitored == request.IsProgramMonitored);
        }

        if (request.IsProgramMissingEpisodes is not null)
        {
            query = query.Where(x => x.HasMissingEpisodes == request.IsProgramMissingEpisodes);
        }

        if (request.IsProgramMatched is not null)
        {
            query = query.Where(x => (x.Series == null) != request.IsProgramMatched);
        }

        if (request.IsProgramPartiallyMatched is true)
        {
            query = query.Where(x =>
                x.Series != null &&
                x.Episodes.Any(e => e.TvdbId != null) &&
                x.Episodes.Any(e => e.TvdbId == null));
        }

        IAsyncEnumerable<ProgramSummary> results = query
            .OrderBy(x => x.Name)
            .Select(x => new ProgramSummary(
                x.Channel,
                x.Name,
                x.RuvId,
                x.IsMonitored,
                x.HasMissingEpisodes,
                x.Series!.Name))
            .AsAsyncEnumerable();

        await foreach (ProgramSummary summary in results.WithCancellation(cancellationToken))
        {
            yield return summary;
        }
    }
}
