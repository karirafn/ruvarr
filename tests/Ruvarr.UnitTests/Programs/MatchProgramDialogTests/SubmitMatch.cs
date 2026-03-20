using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.MatchProgram;
using Ruvarr.Programs.Queries.SearchTvdbSeries;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.MatchProgramDialogTests;

public sealed class SubmitMatch : BunitContext
{
    private readonly IRequestHandler<MatchProgramCommand> _handler;

    public SubmitMatch()
    {
        _handler = Substitute.For<IRequestHandler<MatchProgramCommand>>();
        _handler
            .Handle(Arg.Any<MatchProgramCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddTransient(_ => _handler);

        IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>> searchHandler =
            Substitute.For<IRequestHandler<SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>();
        searchHandler
            .Handle(Arg.Any<SearchTvdbSeriesQuery>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<TvdbSeriesSuggestion>)[]);
        Services.AddTransient(_ => searchHandler);

        Services.AddSingleton(Substitute.For<IJSRuntime>());
    }

    [Fact]
    public async Task DisplaysErrorMessageWhenHandlerReturnsFailure()
    {
        // Arrange
        RuvarrError error = new("Programs.Test", "Something went wrong.");
        _handler
            .Handle(Arg.Any<MatchProgramCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(error));

        IRenderedComponent<MatchProgramDialog> cut = Render<MatchProgramDialog>();
        await cut.Instance.OpenAsync(ruvId: 1, currentTvdbSeriesId: null, programName: "Test Program");
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "12345" });

        // Act
        await cut.Find("button.dialog-button--primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        cut.Markup.ShouldContain("Something went wrong.");
    }

    [Fact]
    public async Task ClearsErrorMessageWhenDialogReopens()
    {
        // Arrange
        RuvarrError error = new("Programs.Test", "Something went wrong.");
        _handler
            .Handle(Arg.Any<MatchProgramCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(error));

        IRenderedComponent<MatchProgramDialog> cut = Render<MatchProgramDialog>();
        await cut.Instance.OpenAsync(ruvId: 1, currentTvdbSeriesId: null, programName: "Test Program");
        await cut.Find("input").InputAsync(new ChangeEventArgs { Value = "12345" });
        await cut.Find("button.dialog-button--primary").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.Markup.ShouldContain("Something went wrong.");

        // Act
        await cut.Instance.OpenAsync(ruvId: 1, currentTvdbSeriesId: null, programName: "Test Program");

        // Assert
        cut.Markup.ShouldNotContain("Something went wrong.");
    }
}
