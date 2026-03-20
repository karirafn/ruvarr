using Bunit;

using Microsoft.JSInterop;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.MatchProgram;
using Ruvarr.Programs.Queries.SearchTvdbSeries;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchProgramDialogTests;

public sealed class DialogTitle : BunitContext
{
    public DialogTitle()
    {
        IRequestHandler<MatchProgramCommand> matchHandler = Substitute.For<IRequestHandler<MatchProgramCommand>>();
        matchHandler
            .Handle(Arg.Any<MatchProgramCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddTransient(_ => matchHandler);

        IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>> searchHandler =
            Substitute.For<IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>();
        searchHandler
            .Handle(Arg.Any<SearchTvdbSeriesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TvdbSeriesSuggestion>)[]);
        Services.AddTransient(_ => searchHandler);

        Services.AddSingleton(Substitute.For<IJSRuntime>());
    }

    [Fact]
    public async Task ShowsMatchToTvdbWhenIsFixMatchIsFalse()
    {
        // Arrange
        IRenderedComponent<MatchProgramDialog> cut = Render<MatchProgramDialog>();

        // Act
        await cut.Instance.OpenAsync(ruvId: 1, currentTvdbSeriesId: null, programName: "Test Program", isFixMatch: false);

        // Assert
        cut.Find("h2").TextContent.ShouldBe("Match to TVDB");
    }

    [Fact]
    public async Task ShowsFixMatchWhenIsFixMatchIsTrue()
    {
        // Arrange
        IRenderedComponent<MatchProgramDialog> cut = Render<MatchProgramDialog>();

        // Act
        await cut.Instance.OpenAsync(ruvId: 1, currentTvdbSeriesId: 12345, programName: "Test Program", isFixMatch: true);

        // Assert
        cut.Find("h2").TextContent.ShouldBe("Fix Match");
    }
}
