using AngleSharp.Dom;

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

public sealed class SonarrColumn : BunitContext
{
    private const int ProgramRuvId = 42;

    private readonly List<EpisodeSummary> _episodes = [];

    public SonarrColumn()
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

        ProgramSummary program = new(
            Channel: "RÚV",
            ProgramName: "Kastljós",
            ProgramRuvId: ProgramRuvId,
            IsMonitored: false,
            HasMissingEpisodes: false,
            SeriesName: null,
            TvdbUrl: null,
            TvdbSeriesId: null,
            RuvUrl: null,
            EpisodeMatchStatus: EpisodeMatchStatus.NoneMatched,
            ImageUrl: null,
            IsMovieMatch: false,
            Description: null,
            EpisodeCount: 0);

        IRequestHandler<GetProgramQuery, ProgramSummary?> programHandler =
            Substitute.For<IRequestHandler<GetProgramQuery, ProgramSummary?>>();
        programHandler
            .Handle(Arg.Any<GetProgramQuery>(), Arg.Any<CancellationToken>())
            .Returns(program);
        Services.AddTransient(_ => programHandler);

        IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>> episodesHandler =
            Substitute.For<IRequestHandler<GetProgramEpisodesQuery, List<EpisodeSummary>>>();
        episodesHandler
            .Handle(Arg.Any<GetProgramEpisodesQuery>(), Arg.Any<CancellationToken>())
            .Returns(_ => _episodes);
        Services.AddTransient(_ => episodesHandler);

        Services.AddTransient(_ => Substitute.For<IRequestHandler<RefreshProgramCommand>>());
        Services.AddTransient<IRequestHandler<Ruvarr.Programs.Commands.MatchProgram.MatchProgramCommand>>(
            _ => Substitute.For<IRequestHandler<Ruvarr.Programs.Commands.MatchProgram.MatchProgramCommand>>());
        Services.AddTransient<IRequestHandler<Ruvarr.Programs.Queries.SearchTvdbSeries.SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>(
            _ => Substitute.For<IRequestHandler<Ruvarr.Programs.Queries.SearchTvdbSeries.SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private void GivenEpisode(SonarrEpisodeStatus? status)
    {
        IReadOnlyList<TvdbEpisodeSummary> matches = status is SonarrEpisodeStatus linkStatus
            ? [new TvdbEpisodeSummary(100, 1, 1, linkStatus, false, null)]
            : [];

        _episodes.Add(new EpisodeSummary(
            EpisodeTitle: "Þáttur 1",
            EpisodeRuvId: "ep1",
            EpisodeDescription: "",
            TvdbMatches: matches,
            FirstRun: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RuvUrl: null,
            Duration: TimeSpan.FromMinutes(30),
            SonarrStatus: status));
    }

    private IElement RenderSonarrCell()
    {
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));
        return cut.Find(".episodes-table tbody tr td:nth-child(3)");
    }

    [Fact]
    public void Header_PlacesSonarrColumnAfterEpisode()
    {
        // Arrange
        GivenEpisode(SonarrEpisodeStatus.Downloaded);

        // Act
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));

        // Assert
        IReadOnlyList<string> headers = [.. cut.FindAll(".episodes-table thead th").Select(th => th.TextContent.Trim())];
        headers.Take(3).ShouldBe(["Season", "Episode", "Sonarr"]);
        cut.FindAll(".episodes-table thead th:not([scope='col'])").ShouldBeEmpty();
    }

    [Fact]
    public void Downloaded_RendersDownloadedIcon()
    {
        // Arrange
        GivenEpisode(SonarrEpisodeStatus.Downloaded);

        // Act
        IElement cell = RenderSonarrCell();

        // Assert
        cell.QuerySelector("span[title='Downloaded in Sonarr'] svg.icon--downloaded[aria-label='Downloaded in Sonarr']").ShouldNotBeNull();
    }

    [Fact]
    public void Missing_RendersMissingIcon()
    {
        // Arrange
        GivenEpisode(SonarrEpisodeStatus.Missing);

        // Act
        IElement cell = RenderSonarrCell();

        // Assert
        cell.QuerySelector("span[title='Missing in Sonarr'] svg.icon--missing[aria-label='Missing in Sonarr']").ShouldNotBeNull();
    }

    [Fact]
    public void NotTracked_RendersDashWithAccessibleText()
    {
        // Arrange
        GivenEpisode(SonarrEpisodeStatus.NotTracked);

        // Act
        IElement cell = RenderSonarrCell();

        // Assert
        IElement dash = cell.QuerySelector("span.unmatched-dash[title='Not tracked by Sonarr']").ShouldNotBeNull();
        dash.QuerySelector("span[aria-hidden='true']")!.TextContent.ShouldBe("—");
        dash.QuerySelector("span.visually-hidden")!.TextContent.ShouldBe("Not tracked by Sonarr");
    }

    [Fact]
    public void Unmatched_RendersEmptyCell()
    {
        // Arrange
        GivenEpisode(status: null);

        // Act
        IElement cell = RenderSonarrCell();

        // Assert
        cell.TextContent.Trim().ShouldBeEmpty();
        cell.Children.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SonarrEpisodeStatus.Downloaded)]
    [InlineData(SonarrEpisodeStatus.Missing)]
    [InlineData(SonarrEpisodeStatus.NotTracked)]
    public void Download_IsOfferedWhateverTheStatus(SonarrEpisodeStatus status)
    {
        // Arrange
        GivenEpisode(status);

        // Act
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));

        // Assert
        cut.FindAll("button[title='Download']").Count.ShouldBe(1);
    }
}