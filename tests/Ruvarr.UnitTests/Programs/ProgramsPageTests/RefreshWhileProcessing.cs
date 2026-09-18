using AngleSharp.Dom;

using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Filters;
using Ruvarr.Programs.Queries.GetPrograms;
using Ruvarr.ProgramRefreshQueue.Notifiers;

using Shouldly;

using ProgramsPage = Ruvarr.Programs.Programs;

namespace Ruvarr.UnitTests.Programs.ProgramsPageTests;

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

        Services.AddSingleton(new ProgramsFilterState());

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

        IStreamingRequestHandler<GetProgramsQuery, ProgramSummary> programsHandler =
            Substitute.For<IStreamingRequestHandler<GetProgramsQuery, ProgramSummary>>();
        programsHandler
            .Handle(Arg.Any<GetProgramsQuery>(), Arg.Any<CancellationToken>())
            .Returns(YieldOne(program));
        Services.AddTransient(_ => programsHandler);

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
        // Arrange — seed Processing state; broadcast a QueueChangedEvent so WatchAsync reads it
        _refreshNotifier.Enqueue(ProgramRuvId, ProgramName);
        _refreshNotifier.MarkProcessing(ProgramRuvId);
        EmitOneQueueChangedEvent();

        // Act
        IRenderedComponent<ProgramsPage> cut = Render<ProgramsPage>();

        // Wait for program list to load and WatchAsync to process the event
        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Refreshing']").Count > 0);

        // Assert — Spinner (Refreshing) is shown; clickable Refresh button is gated away for this item
        cut.FindAll("[aria-label='Refreshing']").Count.ShouldBeGreaterThan(0);
        cut.FindAll("button[title='Refresh']").Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhenProgramIsProcessing_SpinnerSvgPathIsRendered()
    {
        // Arrange
        _refreshNotifier.Enqueue(ProgramRuvId, ProgramName);
        _refreshNotifier.MarkProcessing(ProgramRuvId);
        EmitOneQueueChangedEvent();

        // Act
        IRenderedComponent<ProgramsPage> cut = Render<ProgramsPage>();

        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Refreshing']").Count > 0);

        // Assert — the Spinner SVG path discriminates the Spinner icon type
        IElement spinnerIcon = cut.Find("[aria-label='Refreshing']");
        spinnerIcon.ShouldNotBeNull();
        spinnerIcon.InnerHtml.ShouldContain("M21 12a9 9 0 1 1-6.219-8.56");
    }

    [Fact]
    public async Task WhenProgramIsPending_RendersClock_AndNoRefreshButton()
    {
        // Arrange
        _refreshNotifier.Enqueue(ProgramRuvId, ProgramName);
        EmitOneQueueChangedEvent();

        // Act
        IRenderedComponent<ProgramsPage> cut = Render<ProgramsPage>();

        await cut.WaitForStateAsync(() => cut.FindAll("[aria-label='Pending']").Count > 0);

        // Assert — Clock (Pending) is shown; clickable Refresh button is gated away
        cut.FindAll("[aria-label='Pending']").Count.ShouldBeGreaterThan(0);
        cut.FindAll("button[title='Refresh']").Count.ShouldBe(0);
    }

    [Fact]
    public async Task WhenProgramIsNotQueued_RendersRefreshButton()
    {
        // Arrange — notifier is empty; program is absent from the queue

        // Act
        IRenderedComponent<ProgramsPage> cut = Render<ProgramsPage>();

        // Wait for the program list to load
        await cut.WaitForStateAsync(() => cut.FindAll("button[title='Refresh']").Count > 0);

        // Assert — clickable Refresh button is shown; no processing or queued feedback
        cut.FindAll("button[title='Refresh']").Count.ShouldBe(1);
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
            .Subscribe<Ruvarr.Programs.Events.ProgramCreatedEvent>(Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<Ruvarr.Programs.Events.ProgramCreatedEvent>());
    }

    private static async IAsyncEnumerable<T> YieldOne<T>(T item)
    {
        await Task.Yield();
        yield return item;
    }
}
