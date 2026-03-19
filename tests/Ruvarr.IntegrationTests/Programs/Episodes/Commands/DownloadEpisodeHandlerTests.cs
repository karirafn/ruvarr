using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Commands.DownloadEpisode;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Commands;

public sealed class DownloadEpisodeHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DoesNotAddDuplicateWhenEpisodeAlreadyQueued()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        IRequestHandler<DownloadEpisodeCommand> handler =
            scope.ServiceProvider.GetRequiredService<IRequestHandler<DownloadEpisodeCommand>>();

        RuvProgram program = RuvProgram.Create(1, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("ABC123", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        RuvarrResult result = await handler.Handle(new DownloadEpisodeCommand("ABC123"), cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }
}
