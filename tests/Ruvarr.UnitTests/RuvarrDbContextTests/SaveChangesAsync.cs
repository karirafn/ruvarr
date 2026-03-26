using Microsoft.EntityFrameworkCore;

using Ruvarr.Abstractions;
using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Events;

using Shouldly;

namespace Ruvarr.UnitTests.RuvarrDbContextTests;

public sealed class SaveChangesAsync
{
    [Fact]
    public async Task DispatchesDomainEvents_AfterSavingChanges()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string connectionString = $"Data Source={Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db")}";
        bool programExistedWhenHandlerRan = false;
        SpyEventHandler spy = new(connectionString, saved => programExistedWhenHandlerRan = saved);

        ServiceCollection services = new();
        services.AddDbContext<RuvarrDbContext>(options =>
            options.UseSqlite(connectionString)
                   .UseSnakeCaseNamingConvention());
        services.AddSingleton(spy);
        services.AddSingleton<IDomainEventHandler<ProgramCreatedEvent>>(sp => sp.GetRequiredService<SpyEventHandler>());

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        RuvProgram program = RuvProgram.Create(1, "RÚV1", "Test", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);

        // Act
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assert
        spy.WasInvoked.ShouldBeTrue("Event handler should have been invoked");
        programExistedWhenHandlerRan.ShouldBeTrue("Changes should be saved to DB before domain events are dispatched");
    }

    private sealed class SpyEventHandler(string connectionString, Action<bool> reportSaveStatus) : IDomainEventHandler<ProgramCreatedEvent>
    {
        public bool WasInvoked { get; private set; }

        public async Task Handle(ProgramCreatedEvent @event, CancellationToken cancellationToken)
        {
            WasInvoked = true;

            DbContextOptionsBuilder<RuvarrDbContext> optionsBuilder = new();
            optionsBuilder.UseSqlite(connectionString)
                          .UseSnakeCaseNamingConvention();

            await using ServiceProvider verifyProvider = new ServiceCollection().BuildServiceProvider();
            await using RuvarrDbContext verifyContext = new(optionsBuilder.Options, verifyProvider);
            bool exists = await verifyContext.Set<RuvProgram>().AnyAsync(p => p.RuvId == 1, cancellationToken);
            reportSaveStatus(exists);
        }
    }
}
