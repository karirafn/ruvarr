using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components.Web;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Commands.DismissDownloadQueueItem;
using Ruvarr.Downloads.Commands.RetryDownloadQueueItem;
using Ruvarr.Downloads.Events;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Components;

public sealed class DownloadsTests : BunitContext
{
    private readonly IRequestHandler<GetDownloadQueueQuery, IReadOnlyList<DownloadQueueItemSummary>> _queryHandler =
        Substitute.For<IRequestHandler<GetDownloadQueueQuery, IReadOnlyList<DownloadQueueItemSummary>>>();

    private readonly IRequestHandler<DismissDownloadQueueItemCommand> _dismissHandler =
        Substitute.For<IRequestHandler<DismissDownloadQueueItemCommand>>();

    private readonly IRequestHandler<RetryDownloadQueueItemCommand> _retryHandler =
        Substitute.For<IRequestHandler<RetryDownloadQueueItemCommand>>();

    private readonly DomainEventBroadcaster _broadcaster = new();

    public DownloadsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(_queryHandler);
        Services.AddSingleton(_dismissHandler);
        Services.AddSingleton(_retryHandler);
        Services.AddSingleton<IDomainEventBroadcaster>(_broadcaster);
        _dismissHandler.Handle(Arg.Any<DismissDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        _retryHandler.Handle(Arg.Any<RetryDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
    }

    [Fact]
    public async Task ShowsDismissAfterRetry_OnFailedAndExhaustedRows()
    {
        // Arrange
        ReturnsQueue(
            Item("EP1", DownloadQueueStatus.Failed, "Episode one"),
            Item("EP2", DownloadQueueStatus.Exhausted, "Episode two"),
            Item("EP3", DownloadQueueStatus.Pending, "Episode three"));

        // Act
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            IReadOnlyList<IElement> rows = cut.FindAll("tbody tr");
            rows.Count.ShouldBe(3);
            ButtonClasses(rows[0]).ShouldBe(["downloads-retry-button", "downloads-dismiss-button"]);
            ButtonClasses(rows[1]).ShouldBe(["downloads-retry-button", "downloads-dismiss-button"]);
            ButtonClasses(rows[2]).ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task DismissButton_HasLabelStartingWithVisibleText()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));

        // Act
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            IElement button = cut.Find("button.downloads-dismiss-button");
            button.TextContent.Trim().ShouldBe("Dismiss");
            button.GetAttribute("aria-label").ShouldBe("Dismiss Episode one from the download queue");
        });
    }

    [Fact]
    public async Task DismissButton_DescribesEpisodeByProgram_WhenTitleIsBlank()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, " "));

        // Act
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();

        // Assert
        await cut.WaitForAssertionAsync(() =>
            cut.Find("button.downloads-dismiss-button").GetAttribute("aria-label")
                .ShouldBe("Dismiss episode from Show from the download queue"));
    }

    [Fact]
    public async Task ClickingDismiss_SendsCommandForEpisode()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");

        // Act
        await cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());

        // Assert
        await _dismissHandler.Received(1).Handle(new DismissDownloadQueueItemCommand("EP1"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuccessfulDismiss_ShowsStatusMessageLinkingToProgram()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");
        ReturnsQueue();

        // Act
        await cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            IElement status = cut.Find(".downloads-action-status[role='status']");
            status.TextContent.ShouldBe("Dismissed Episode one. To download it again, open Show.");
            status.QuerySelector("a.program-link")!.GetAttribute("href").ShouldBe("/program/42");
            cut.Find(".queue-delete-error[role='alert']").TextContent.ShouldBeEmpty();
            cut.FindAll("tbody tr").ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task FailedDismiss_ShowsErrorDescription_AndNoStatusMessage()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        _dismissHandler.Handle(Arg.Any<DismissDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(DownloadErrors.ItemNotDismissable);
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");

        // Act
        await cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find(".queue-delete-error[role='alert']").TextContent.ShouldBe("Only Failed or Exhausted items can be dismissed.");
            cut.Find(".downloads-action-status").TextContent.ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task WhileDismissing_LocksBothButtons_AndMarksDismissBusy()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        TaskCompletionSource<RuvarrResult> pending = new();
        _dismissHandler.Handle(Arg.Any<DismissDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(pending.Task);
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");

        // Act
        Task click = cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            IElement dismiss = cut.Find("button.downloads-dismiss-button");
            IElement retry = cut.Find("button.downloads-retry-button");
            dismiss.HasAttribute("disabled").ShouldBeTrue();
            dismiss.GetAttribute("aria-busy").ShouldBe("true");
            dismiss.TextContent.Trim().ShouldBe("Dismissing...");
            retry.HasAttribute("disabled").ShouldBeTrue();
            retry.HasAttribute("aria-busy").ShouldBeFalse();
        });

        pending.SetResult(RuvarrResult.Success);
        await click;
    }

    [Fact]
    public async Task HandlerException_UnlocksRow()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        _dismissHandler.Handle(Arg.Any<DismissDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns<RuvarrResult>(_ => throw new InvalidOperationException("boom"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");

        // Act
        await Should.ThrowAsync<InvalidOperationException>(
            () => cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs()));

        // Assert
        cut.Render();
        cut.Find("button.downloads-dismiss-button").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task SuccessfulDismiss_RequestsFocus_SoKeyboardUsersAreNotStranded()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");
        ReturnsQueue();

        // Act
        await cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() =>
        {
            cut.Find("h1").GetAttribute("tabindex").ShouldBe("-1");
            JSInterop.VerifyFocusAsyncInvoke();
        });
    }

    [Fact]
    public async Task StartingRetry_ClearsPreviousDismissMessage()
    {
        // Arrange
        ReturnsQueue(
            Item("EP1", DownloadQueueStatus.Failed, "Episode one"),
            Item("EP2", DownloadQueueStatus.Failed, "Episode two"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");
        ReturnsQueue(Item("EP2", DownloadQueueStatus.Failed, "Episode two"));
        await cut.Find("button.downloads-dismiss-button").ClickAsync(new MouseEventArgs());
        await cut.WaitForAssertionAsync(() => cut.Find(".downloads-action-status").TextContent.ShouldNotBeEmpty());

        // Act
        await cut.Find("button.downloads-retry-button").ClickAsync(new MouseEventArgs());

        // Assert
        await cut.WaitForAssertionAsync(() => cut.Find(".downloads-action-status").TextContent.ShouldBeEmpty());
    }

    [Fact]
    public async Task ReloadsQueue_WhenDownloadDismissedEventIsBroadcast()
    {
        // Arrange
        ReturnsQueue(Item("EP1", DownloadQueueStatus.Failed, "Episode one"));
        IRenderedComponent<Ruvarr.Downloads.Downloads> cut = Render<Ruvarr.Downloads.Downloads>();
        await cut.WaitForElementAsync("button.downloads-dismiss-button");
        ReturnsQueue();

        // Act
        _broadcaster.Publish(new DownloadDismissedEvent(null!));

        // Assert
        await cut.WaitForAssertionAsync(() => cut.FindAll("tbody tr").ShouldBeEmpty());
    }

    private void ReturnsQueue(params DownloadQueueItemSummary[] items) =>
        _queryHandler.Handle(Arg.Any<GetDownloadQueueQuery>(), Arg.Any<CancellationToken>())
            .Returns(items);

    private static DownloadQueueItemSummary Item(string episodeRuvId, DownloadQueueStatus status, string title) =>
        new(episodeRuvId, 42, "Show", title, status, null, 1, null, DateTime.UtcNow);

    private static List<string> ButtonClasses(IElement row) =>
        [.. row.QuerySelectorAll("button").Select(b => b.ClassList.First(c => c.StartsWith("downloads-", StringComparison.Ordinal)))];
}
