using Bunit;

using Ruvarr.Contracts;
using Ruvarr.Programs.Components;

namespace Ruvarr.UnitTests.Programs.EpisodeComboboxTests;

public sealed class Dispose : BunitContext
{
    private static readonly IReadOnlyList<TvdbSeriesEpisode> TestEpisodes =
    [
        new(TvdbId: 1, Name: "Pilot", SeasonNumber: 1, EpisodeNumber: 1),
    ];

    public Dispose()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenDisposed_InvokesUnregisterComboboxKeys()
    {
        // Arrange
        IRenderedComponent<EpisodeCombobox> cut = Render<EpisodeCombobox>(parameters => parameters
            .Add(p => p.Episodes, TestEpisodes)
            .Add(p => p.DefaultSeason, 1));

        // Act
        await cut.Instance.DisposeAsync();

        // Assert
        JSInterop.VerifyInvoke("unregisterComboboxKeys");
    }
}
