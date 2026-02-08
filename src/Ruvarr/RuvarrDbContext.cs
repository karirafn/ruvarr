using Microsoft.EntityFrameworkCore;

namespace Ruvarr;

internal sealed class RuvarrDbContext : DbContext
{
    public RuvarrDbContext(DbContextOptions<RuvarrDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RuvarrDbContext).Assembly);
    }
}