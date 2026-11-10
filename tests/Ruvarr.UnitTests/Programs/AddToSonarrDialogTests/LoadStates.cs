using AngleSharp.Dom;

using Bunit;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs.Commands.AddProgramToSonarr;
using Ruvarr.Programs.Components;
using Ruvarr.Programs.Queries.GetSonarrQualityProfiles;
using Ruvarr.Programs.Queries.GetSonarrRootFolders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.AddToSonarrDialogTests;

public sealed class LoadStates : BunitContext
{
    private const string UnreachableMessage =
        "Could not connect to Sonarr. Check the Sonarr URL and API key in Settings, then retry.";
    private const string NoRootFoldersMessage =
        "Sonarr has no root folders. Add one in Sonarr under Settings → Media Management, then reload.";
    private const string NoQualityProfilesMessage =
        "Sonarr has no quality profiles. Add one in Sonarr under Settings → Profiles, then reload.";

    private static readonly Result<IReadOnlyList<RootFolder>> RootFoldersFailure = new(ApiClientErrors.RequestFailed);
    private static readonly Result<IReadOnlyList<QualityProfile>> QualityProfilesFailure = new(ApiClientErrors.RequestFailed);
    private static readonly Result<IReadOnlyList<RootFolder>> SomeRootFolders = new([new RootFolder(1, "/tv")]);
    private static readonly Result<IReadOnlyList<QualityProfile>> SomeQualityProfiles = new([new QualityProfile(7, "HD-1080p")]);
    private static readonly Result<IReadOnlyList<RootFolder>> NoRootFolders = new(Array.Empty<RootFolder>());
    private static readonly Result<IReadOnlyList<QualityProfile>> NoQualityProfiles = new(Array.Empty<QualityProfile>());

    private readonly IRequestHandler<GetSonarrRootFoldersQuery, Result<IReadOnlyList<RootFolder>>> _rootFolders =
        Substitute.For<IRequestHandler<GetSonarrRootFoldersQuery, Result<IReadOnlyList<RootFolder>>>>();
    private readonly IRequestHandler<GetSonarrQualityProfilesQuery, Result<IReadOnlyList<QualityProfile>>> _qualityProfiles =
        Substitute.For<IRequestHandler<GetSonarrQualityProfilesQuery, Result<IReadOnlyList<QualityProfile>>>>();
    private readonly IRequestHandler<AddProgramToSonarrCommand> _addHandler =
        Substitute.For<IRequestHandler<AddProgramToSonarrCommand>>();

    public LoadStates()
    {
        Services.AddTransient(_ => _rootFolders);
        Services.AddTransient(_ => _qualityProfiles);
        Services.AddTransient(_ => _addHandler);
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private async Task<IRenderedComponent<AddToSonarrDialog>> OpenAsync(
        Result<IReadOnlyList<RootFolder>> rootFolders,
        Result<IReadOnlyList<QualityProfile>> qualityProfiles)
    {
        _rootFolders.Handle(Arg.Any<GetSonarrRootFoldersQuery>(), Arg.Any<CancellationToken>()).Returns(rootFolders);
        _qualityProfiles.Handle(Arg.Any<GetSonarrQualityProfilesQuery>(), Arg.Any<CancellationToken>()).Returns(qualityProfiles);
        IRenderedComponent<AddToSonarrDialog> cut = Render<AddToSonarrDialog>();
        await cut.InvokeAsync(() => cut.Instance.OpenAsync(ruvId: 1));
        return cut;
    }

    private static IElement RootFolderSelect(IRenderedComponent<AddToSonarrDialog> cut) => cut.FindAll("select")[0];

    private static IElement QualityProfileSelect(IRenderedComponent<AddToSonarrDialog> cut) => cut.FindAll("select")[1];

    private static IElement ConfirmButton(IRenderedComponent<AddToSonarrDialog> cut) => cut.Find(".dialog-button--primary");

    private static string[] Alerts(IRenderedComponent<AddToSonarrDialog> cut) =>
        [.. cut.FindAll("[role=alert]").Select(e => e.TextContent.Trim())];

    private static string[] ButtonLabels(IRenderedComponent<AddToSonarrDialog> cut) =>
        [.. cut.FindAll("button").Select(e => e.TextContent.Trim())];

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task WhenEitherQueryFails_ShowsUnreachableMessageWithRetry(bool rootFoldersFail, bool qualityProfilesFail)
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(
            rootFoldersFail ? RootFoldersFailure : SomeRootFolders,
            qualityProfilesFail ? QualityProfilesFailure : SomeQualityProfiles);

        // Assert
        Alerts(cut).ShouldBe([UnreachableMessage]);
        ButtonLabels(cut).ShouldContain("Retry");
        cut.Find("[role=alert] a").GetAttribute("href").ShouldBe("/settings");
        RootFolderSelect(cut).HasAttribute("disabled").ShouldBeTrue();
        QualityProfileSelect(cut).HasAttribute("disabled").ShouldBeTrue();
        RootFolderSelect(cut).GetAttribute("aria-describedby").ShouldBe("sonarr-load-error");
        QualityProfileSelect(cut).GetAttribute("aria-describedby").ShouldBe("sonarr-load-error");
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenOneQueryFailsAndTheOtherIsEmpty_UnreachableTakesPriority()
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(RootFoldersFailure, NoQualityProfiles);

        // Assert
        Alerts(cut).ShouldBe([UnreachableMessage]);
        ButtonLabels(cut).ShouldNotContain("Reload");
    }

    [Fact]
    public async Task WhenNoRootFolders_ShowsRootFolderMessageWithReload()
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(NoRootFolders, SomeQualityProfiles);

        // Assert
        cut.Find("#root-folder-error").TextContent.Trim().ShouldBe(NoRootFoldersMessage);
        Alerts(cut).ShouldBe([NoRootFoldersMessage]);
        ButtonLabels(cut).Count(l => l == "Reload").ShouldBe(1);
        IElement select = RootFolderSelect(cut);
        select.HasAttribute("disabled").ShouldBeTrue();
        select.GetAttribute("aria-describedby").ShouldBe("root-folder-error");
        select.QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["No root folders in Sonarr"]);
        QualityProfileSelect(cut).HasAttribute("disabled").ShouldBeFalse();
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenNoQualityProfiles_ShowsQualityProfileMessageWithReload()
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(SomeRootFolders, NoQualityProfiles);

        // Assert
        cut.Find("#quality-profile-error").TextContent.Trim().ShouldBe(NoQualityProfilesMessage);
        Alerts(cut).ShouldBe([NoQualityProfilesMessage]);
        ButtonLabels(cut).Count(l => l == "Reload").ShouldBe(1);
        IElement select = QualityProfileSelect(cut);
        select.HasAttribute("disabled").ShouldBeTrue();
        select.GetAttribute("aria-describedby").ShouldBe("quality-profile-error");
        select.QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["No quality profiles in Sonarr"]);
        RootFolderSelect(cut).HasAttribute("disabled").ShouldBeFalse();
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenNoRootFoldersAndNoQualityProfiles_ShowsBothMessagesWithOneReload()
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(NoRootFolders, NoQualityProfiles);

        // Assert
        Alerts(cut).ShouldBe([NoRootFoldersMessage, NoQualityProfilesMessage]);
        ButtonLabels(cut).Count(l => l == "Reload").ShouldBe(1);
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task WhenLoaded_ShowsPlaceholdersAndNoMessages()
    {
        // Act
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(SomeRootFolders, SomeQualityProfiles);

        // Assert
        Alerts(cut).ShouldBeEmpty();
        RootFolderSelect(cut).QuerySelector("option")!.TextContent.ShouldBe("-- Select root folder --");
        QualityProfileSelect(cut).QuerySelector("option")!.TextContent.ShouldBe("-- Select quality profile --");
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task RetryAfterRecovery_LoadsAndFocusesFirstSelect()
    {
        // Arrange
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(RootFoldersFailure, SomeQualityProfiles);
        _rootFolders.Handle(Arg.Any<GetSonarrRootFoldersQuery>(), Arg.Any<CancellationToken>()).Returns(SomeRootFolders);

        // Act
        await cut.Find("button.dialog-button:not(.dialog-button--primary)").ClickAsync(new());

        // Assert
        Alerts(cut).ShouldBeEmpty();
        RootFolderSelect(cut).HasAttribute("disabled").ShouldBeFalse();
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task SubmitWhenSonarrUnreachable_ShowsSubmitMessageAndKeepsConfirmEnabled()
    {
        // Arrange
        _addHandler.Handle(Arg.Any<AddProgramToSonarrCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(SonarrErrors.Unreachable));
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(SomeRootFolders, SomeQualityProfiles);
        await RootFolderSelect(cut).ChangeAsync(new() { Value = "1" });
        await QualityProfileSelect(cut).ChangeAsync(new() { Value = "7" });

        // Act
        await ConfirmButton(cut).ClickAsync(new());

        // Assert
        Alerts(cut).ShouldBe(["Could not connect to Sonarr. Check that Sonarr is running, then try again."]);
        ButtonLabels(cut).ShouldNotContain("Reload");
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task SubmitWithStaleRootFolder_ShowsReloadPrompt()
    {
        // Arrange
        _addHandler.Handle(Arg.Any<AddProgramToSonarrCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Failure(SonarrErrors.InvalidRootFolderPath));
        IRenderedComponent<AddToSonarrDialog> cut = await OpenAsync(SomeRootFolders, SomeQualityProfiles);
        await RootFolderSelect(cut).ChangeAsync(new() { Value = "1" });
        await QualityProfileSelect(cut).ChangeAsync(new() { Value = "7" });

        // Act
        await ConfirmButton(cut).ClickAsync(new());

        // Assert
        Alerts(cut).ShouldBe(["The selected root folder is no longer in Sonarr. Reload and choose another."]);
        ButtonLabels(cut).ShouldContain("Reload");
        ConfirmButton(cut).HasAttribute("disabled").ShouldBeFalse();
    }
}
