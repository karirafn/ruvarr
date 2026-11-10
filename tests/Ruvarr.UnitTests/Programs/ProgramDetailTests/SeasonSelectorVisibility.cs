using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Queries.GetProgram;
using Ruvarr.Programs.Queries.GetProgramEpisodes;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramDetailTests;

public sealed class SeasonSelectorVisibility : BunitContext
{
    private const int ProgramRuvId = 42;

    private readonly IRequestHandler<GetProgramQuery, ProgramSummary?> _programHandler =
        Substitute.For<IRequestHandler<GetProgramQuery, ProgramSummary?>>();

    public SeasonSelectorVisibility()
    {
        Services.AddSingleton(new ProgramRefreshNotifier(TimeProvider.System));

        IDomainEventBroadcaster broadcaster = Substitute.For<IDomainEventBroadcaster>();
        broadcaster
            .Subscribe<QueueChangedEvent<ProgramRefreshQueueItemSummary>>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<QueueChangedEvent<ProgramRefreshQueueItemSummary>>());
        broadcaster
            .Subscribe<Ruvarr.Downloads.Events.DownloadCompletedEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Downloads.Events.DownloadCompletedEvent>());
        broadcaster
            .Subscribe<Ruvarr.Programs.Events.EpisodeMatchedEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.EpisodeMatchedEvent>());
        broadcaster
            .Subscribe<Ruvarr.Programs.Events.ProgramMatchedTvdbEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.ProgramMatchedTvdbEvent>());
        broadcaster
            .Subscribe<Ruvarr.Programs.Events.ProgramMatchedTmdbEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.ProgramMatchedTmdbEvent>());
        Services.AddSingleton(broadcaster);

        Services.AddTransient(_ => _programHandler);

        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> episodesHandler =
            Substitute.For<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();
        episodesHandler
            .Handle(Arg.Any<GetProgramEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => []);
        Services.AddTransient(_ => episodesHandler);
        Services.AddTransient(_ => Substitute.For<IRequestHandler<RefreshProgramCommand>>());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void GivenProgram(string? seriesName, int? tvdbSeriesId, bool isMovieMatch, IReadOnlyList<int> manualSeasons) =>
        _programHandler
            .Handle(Arg.Any<GetProgramQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ProgramSummary(
                Channel: "RÚV",
                ProgramName: "Ráðherrann II",
                ProgramRuvId: ProgramRuvId,
                IsMonitored: false,
                HasMissingEpisodes: false,
                SeriesName: seriesName,
                TvdbUrl: null,
                TvdbSeriesId: tvdbSeriesId,
                RuvUrl: null,
                EpisodeMatchStatus: EpisodeMatchStatus.NoneMatched,
                ImageUrl: null,
                IsMovieMatch: isMovieMatch,
                Description: null,
                EpisodeCount: 0,
                ManualSeasons: manualSeasons,
                ParsedSeasonToken: "II",
                ParsedSeasonNumber: 2));

    private IRenderedComponent<ProgramDetail> RenderDetail() =>
        Render<ProgramDetail>(parameters => parameters.Add(p => p.RuvId, ProgramRuvId));

    [Fact]
    public void ShowsTheSelectorForASeriesMatch()
    {
        // Arrange
        GivenProgram("Ráðherrann", 1000, isMovieMatch: false, manualSeasons: [1, 2]);

        // Act
        IRenderedComponent<ProgramDetail> cut = RenderDetail();

        // Assert
        cut.Find(".series-meta .season-selector-trigger").TextContent.Trim().ShouldBe("Season: 1, 2");
    }

    [Fact]
    public void HidesTheSelectorWithoutATvdbSeries()
    {
        // Arrange
        GivenProgram(null, null, isMovieMatch: false, manualSeasons: []);

        // Act
        IRenderedComponent<ProgramDetail> cut = RenderDetail();

        // Assert
        cut.FindAll(".season-selector-trigger").ShouldBeEmpty();
    }

    [Fact]
    public void HidesTheSelectorForAMovieMatch()
    {
        // Arrange
        GivenProgram("Ráðherrann", 1000, isMovieMatch: true, manualSeasons: []);

        // Act
        IRenderedComponent<ProgramDetail> cut = RenderDetail();

        // Assert
        cut.FindAll(".season-selector-trigger").ShouldBeEmpty();
    }
}
