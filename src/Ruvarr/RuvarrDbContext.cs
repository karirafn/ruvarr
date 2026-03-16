using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;

namespace Ruvarr;

public sealed class RuvarrDbContext(DbContextOptions<RuvarrDbContext> options, IServiceProvider serviceProvider)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RuvarrDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        List<IDomainEvent> events = [
            .. ChangeTracker.Entries<RuvEpisode>().SelectMany(e => e.Entity.DomainEvents),
            .. ChangeTracker.Entries<RuvProgram>().SelectMany(e => e.Entity.DomainEvents),
        ];

        foreach (EntityEntry<RuvEpisode> entry in ChangeTracker.Entries<RuvEpisode>().ToList())
        {
            entry.Entity.ClearDomainEvents();
        }

        foreach (EntityEntry<RuvProgram> entry in ChangeTracker.Entries<RuvProgram>().ToList())
        {
            entry.Entity.ClearDomainEvents();
        }

        foreach (IDomainEvent @event in events)
        {
            Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(@event.GetType());
            foreach (IDomainEventHandler handler in serviceProvider.GetServices(handlerType).OfType<IDomainEventHandler>())
            {
                await handler.Handle(@event, cancellationToken);
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}