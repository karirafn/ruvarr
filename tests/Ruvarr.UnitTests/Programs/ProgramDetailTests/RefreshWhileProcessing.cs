using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Queries.GetProgram;
using Ruvarr.Programs.Queries.GetProgramEpisodes;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.ProgramDetailTests;

public sealed class RefreshWhileProcessing : BunitContext
{
    private const int ProgramRuvId = 42;
    private const string ProgramName = "Kastljós";

    private readonly ProgramRefreshNotifier _refreshNotifier;
    private readonly IDomainEventBroadcaster _broadcaster;

    public RefreshWhileProcessing()
    {
        _refreshNotifier = new ProgramRefreshNotifier();
        Services.AddSingleton(_refreshNotifier);

        _broadcaster = Substitute.For<IDomainEventBroadcaster>();
        SetupBroadcasterDefaults();
        Services.AddSingleton(_broadcaster);

        ProgramSummary program = new(
            Channel: "RÚV",
            ProgramName: ProgramName,
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
            .Returns([]);
        Services.AddTransient(_ => episodesHandler);

        IRequestHandler<RefreshProgramCommand> refreshHandler =
            Substitute.For<IRequestHandler<RefreshProgramCommand>>();
        refreshHandler
            .Handle(Arg.Any<RefreshProgramCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddTransient(_ => refreshHandler);

        Services.AddTransient<IRequestHandler<Ruvarr.Programs.Commands.MatchProgram.MatchProgramCommand>>(
            _ => Substitute.For<IRequestHandler<Ruvarr.Programs.Commands.MatchProgram.MatchProgramCommand>>());
        Services.AddTransient<IRequestHandler<Ruvarr.Programs.Queries.SearchTvdbSeries.SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>(
            _ => Substitute.For<IRequestHandler<Ruvarr.Programs.Queries.SearchTvdbSeries.SearchTvdbSeriesQuery, IReadOnlyList<TvdbSeriesSuggestion>>>());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task WhenProgramIsProcessing_RendersSpinner_AndNoRefreshButton()
    {
        // Arrange — seed Processing state; broadcast a QueueChangedEvent so WatchRefreshQueueAsync reads it
        _refreshNotifier.Enqueue(ProgramRuvId, ProgramName);
        _refreshNotifier.MarkProcessing(ProgramRuvId);
        EmitOneQueueChangedEvent();

        // Act
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));

        // Wait for WatchRefreshQueueAsync to process the event and call StateHasChanged
        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Refreshing']").Count > 0);

        // Assert — Spinner (Refreshing) is shown; clickable Refresh button is gated away
        cut.FindAll("[aria-label='Refreshing']").Count.ShouldBeGreaterThan(0);
        cut.FindAll("button[title='Refresh program']").Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhenProgramIsPending_RendersClock_AndNoRefreshButton()
    {
        // Arrange
        _refreshNotifier.Enqueue(ProgramRuvId, ProgramName);
        EmitOneQueueChangedEvent();

        // Act
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));

        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Pending']").Count > 0);

        // Assert — Clock (Pending) is shown; clickable Refresh button is gated away
        cut.FindAll("[aria-label='Pending']").Count.ShouldBeGreaterThan(0);
        cut.FindAll("button[title='Refresh program']").Count.ShouldBe(0);
    }

    [Fact]
    public void WhenProgramIsNotQueued_RendersRefreshButton()
    {
        // Arrange — notifier is empty; program is absent from the queue

        // Act
        IRenderedComponent<ProgramDetail> cut = Render<ProgramDetail>(parameters =>
            parameters.Add(p => p.RuvId, ProgramRuvId));

        // Assert — clickable Refresh button is shown; no processing or queued feedback
        cut.FindAll("button[title='Refresh program']").Count.ShouldBe(1);
        cut.FindAll("[aria-label='Refreshing']").Count.ShouldBe(0);
        cut.FindAll("[aria-label='Pending']").Count.ShouldBe(0);
    }

    private void EmitOneQueueChangedEvent()
    {
        _broadcaster
            .Subscribe<QueueChangedEvent<ProgramRefreshQueueItemSummary>>(Arg.Any<CancellationToken>())
            .Returns(YieldOne(new QueueChangedEvent<ProgramRefreshQueueItemSummary>()));
    }

    private void SetupBroadcasterDefaults()
    {
        _broadcaster
            .Subscribe<QueueChangedEvent<ProgramRefreshQueueItemSummary>>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<QueueChangedEvent<ProgramRefreshQueueItemSummary>>());
        _broadcaster
            .Subscribe<Ruvarr.Downloads.Events.DownloadCompletedEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Downloads.Events.DownloadCompletedEvent>());
        _broadcaster
            .Subscribe<Ruvarr.Programs.Events.EpisodeMatchedEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.EpisodeMatchedEvent>());
        _broadcaster
            .Subscribe<Ruvarr.Programs.Events.ProgramMatchedTvdbEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.ProgramMatchedTvdbEvent>());
        _broadcaster
            .Subscribe<Ruvarr.Programs.Events.ProgramMatchedTmdbEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.ProgramMatchedTmdbEvent>());
    }

    private static async IAsyncEnumerable<T> YieldOne<T>(T item)
    {
        await Task.Yield();
        yield return item;
    }
}
