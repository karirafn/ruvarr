using System.Net;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Downloads;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Commands;

public sealed class DownloadEpisodeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task DoesNotAddDuplicateWhenEpisodeAlreadyQueued()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RÚV1", "Test Program", null, multipleEpisodes: true);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("ABC123", new Uri("https://example.com/ep.mp4"), "Test Episode", "Description", DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        dbContext.EnqueueDownload(episode);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Act
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsync("/programs/episodes/ABC123/download", content: null, cancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }
}
