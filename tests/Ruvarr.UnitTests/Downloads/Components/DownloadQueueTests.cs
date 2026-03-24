using AngleSharp.Dom;

using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Contracts;
using Ruvarr.Downloads.Commands.DeleteDownloadQueueItem;
using Ruvarr.Downloads;
using Ruvarr.Downloads.Components;
using Ruvarr.Downloads.Queries.GetDownloadQueue;

using Shouldly;

namespace Ruvarr.UnitTests.Downloads.Components;

public sealed class DownloadQueueTests : BunitContext
{
    [Fact]
    public void RendersDeleteButton_ForPendingItem()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP001", "Episode 1", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Pending),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        // Act
        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Assert
        IElement button = cut.Find("button.icon-button--danger");
        button.ShouldNotBeNull();
        button.GetAttribute("title").ShouldBe("Remove from queue");
        button.GetAttribute("aria-label").ShouldBe("Remove from queue");
    }

    [Fact]
    public void RendersDeleteButton_ForFailedItem()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP002", "Episode 2", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Failed),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        // Act
        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Assert
        IElement button = cut.Find("button.icon-button--danger");
        button.ShouldNotBeNull();
    }

    [Fact]
    public void DoesNotRenderDeleteButton_ForDownloadingItem()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP003", "Episode 3", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Downloading),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        // Act
        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Assert
        cut.FindAll("button.icon-button--danger").ShouldBeEmpty();
    }

    [Fact]
    public void DoesNotRenderDeleteButton_ForCompleteItem()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP004", "Episode 4", "Program 1", DateTime.UtcNow, DateTime.UtcNow, DownloadQueueStatus.Complete),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        // Act
        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Assert
        cut.FindAll("button.icon-button--danger").ShouldBeEmpty();
    }

    [Fact]
    public void RendersActionsColumnHeader_WithVisuallyHiddenLabel()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP001", "Episode 1", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Pending),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        // Act
        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Assert
        cut.FindAll("thead th").Count.ShouldBe(5);
        IElement actionsHeader = cut.FindAll("thead th")[4];
        IElement hiddenLabel = actionsHeader.QuerySelector("span.visually-hidden")!;
        hiddenLabel.ShouldNotBeNull();
        hiddenLabel.TextContent.ShouldBe("Actions");
    }

    [Fact]
    public async Task FirstClickShowsConfirmationState()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP001", "Episode 1", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Pending),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler();
        RegisterBroadcaster();

        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Act
        await cut.Find("button.icon-button--danger").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        IElement confirmButton = cut.Find("button.icon-button--confirming");
        confirmButton.ShouldNotBeNull();
        confirmButton.GetAttribute("title").ShouldBe("Click again to confirm");
        confirmButton.GetAttribute("aria-label").ShouldBe("Confirm removal");
    }

    [Fact]
    public async Task SecondClickExecutesDelete()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP001", "Episode 1", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Pending),
        ];

        RegisterQueryHandler(items);
        IRequestHandler<DeleteDownloadQueueItemCommand> deleteHandler = RegisterDeleteHandler();
        RegisterBroadcaster();

        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Act
        await cut.Find("button.icon-button--danger").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.icon-button--confirming").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        await deleteHandler.Received(1)
            .Handle(Arg.Is<DeleteDownloadQueueItemCommand>(c => c.EpisodeRuvId == "EP001"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisplaysErrorMessage_WhenDeleteFails()
    {
        // Arrange
        List<DownloadQueueItemSummary> items =
        [
            new("EP001", "Episode 1", "Program 1", DateTime.UtcNow, null, DownloadQueueStatus.Pending),
        ];

        RegisterQueryHandler(items);
        RegisterDeleteHandler(RuvarrResult.Failure(DownloadErrors.ItemNotDeletable));
        RegisterBroadcaster();

        IRenderedComponent<DownloadQueue> cut = Render<DownloadQueue>();

        // Act
        await cut.Find("button.icon-button--danger").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        await cut.Find("button.icon-button--confirming").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        // Assert
        IElement errorSpan = cut.Find("span.queue-delete-error");
        errorSpan.ShouldNotBeNull();
        errorSpan.TextContent.ShouldBe("Only pending or failed items can be deleted.");
    }

    private void RegisterQueryHandler(List<DownloadQueueItemSummary> items)
    {
        IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>> handler =
            Substitute.For<IRequestHandler<GetDownloadQueueQuery, List<DownloadQueueItemSummary>>>();
        handler.Handle(Arg.Any<GetDownloadQueueQuery>(), Arg.Any<CancellationToken>())
            .Returns(items);
        Services.AddSingleton(handler);
    }

    private IRequestHandler<DeleteDownloadQueueItemCommand> RegisterDeleteHandler(RuvarrResult? result = null)
    {
        IRequestHandler<DeleteDownloadQueueItemCommand> handler =
            Substitute.For<IRequestHandler<DeleteDownloadQueueItemCommand>>();
        handler.Handle(Arg.Any<DeleteDownloadQueueItemCommand>(), Arg.Any<CancellationToken>())
            .Returns(result ?? RuvarrResult.Success);
        Services.AddSingleton(handler);
        return handler;
    }

    private void RegisterBroadcaster()
    {
        IDomainEventBroadcaster broadcaster = Substitute.For<IDomainEventBroadcaster>();
        Services.AddSingleton(broadcaster);
    }
}
