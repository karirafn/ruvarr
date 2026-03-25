using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr.Settings.Queries.GetDistinctChannels;

internal sealed class GetDistinctChannelsHandler(RuvarrDbContext dbContext)
    : IRequestHandler<GetDistinctChannelsQuery, List<string>>
{
    public async Task<List<string>> Handle(GetDistinctChannelsQuery request, CancellationToken cancellationToken)
    {
        return await dbContext
            .Set<RuvProgram>()
            .Select(p => p.Channel)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
