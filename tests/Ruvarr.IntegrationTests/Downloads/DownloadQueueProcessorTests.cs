using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Downloads.Domain;
using Ruvarr.Jobs;

using Shouldly;

namespace Ruvarr.IntegrationTests.Downloads;

public sealed class DownloadQueueProcessorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task RemovesOrphanedItemWithNoEpisode()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        // Seed an orphaned item (no episode row points to it)
        await dbContext.Database.ExecuteSqlAsync(
            $"INSERT INTO download_queue (created, status) VALUES ({DateTime.UtcNow.ToString("o")}, 'Pending')",
            cancellationToken);

        // Act
        DownloadQueueProcessor processor = ActivatorUtilities.CreateInstance<DownloadQueueProcessor>(scope.ServiceProvider);
        await processor.Execute(null!);

        // Assert - orphaned item should be removed
        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }
}
