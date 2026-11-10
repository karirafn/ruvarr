using Microsoft.Extensions.Caching.Memory;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.UnitTests.Infrastructure.Sonarr.CachingSonarrClientTests;

public sealed class InvalidateMissingEpisodes
{
    [Fact]
    public async Task NextMissingEpisodesCallReadsSonarrAgain()
    {
        // Arrange
        ISonarrClient innerClient = Substitute.For<ISonarrClient>();
        innerClient.GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new Result<IReadOnlyCollection<MissingEpisode>>([]));
        using MemoryCache cache = new(new MemoryCacheOptions());
        using CachingSonarrClient sut = new(() => innerClient, cache);
        await sut.GetMissingEpisodesAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Act
        sut.InvalidateMissingEpisodes();
        await sut.GetMissingEpisodesAsync(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        await innerClient.Received(2).GetMissingEpisodesAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
