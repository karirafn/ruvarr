
using System.Net;

using Shouldly;

namespace Ruvarr.IntegrationTests.Programs.Episodes.Queries;

public sealed class GetEpisodes(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    [Fact]
    public async Task ReturnsOk()
    {
        // Arrange

        // Act
        HttpResponseMessage result = await factory.CreateClient().GetAsync("/programs/episodes", CancellationToken.None);

        // Assert
        result.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
