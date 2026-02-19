using Microsoft.EntityFrameworkCore;

namespace Ruvarr;

public sealed class RuvarrDbContext(DbContextOptions<RuvarrDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RuvarrDbContext).Assembly);
    }
}