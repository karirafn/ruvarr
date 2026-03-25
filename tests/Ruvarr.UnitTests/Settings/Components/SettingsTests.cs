using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Settings;
using Ruvarr.Settings.Commands.SaveSettings;
using Ruvarr.Settings.Queries.GetSettings;
using Ruvarr.Settings.Queries.TestSonarrConnection;

using Shouldly;

namespace Ruvarr.UnitTests.Settings.Components;

public sealed class SettingsTests : BunitContext
{
    [Fact]
    public void RendersExistingIgnoredChannelsAsTags()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1", "RUV2"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        // Act
        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(2);
        tags[0].TextContent.ShouldContain("RUV1");
        tags[1].TextContent.ShouldContain("RUV2");
    }

    [Fact]
    public async Task AddsChannelViaInputAndButton()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "NewChannel" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
        tags[0].TextContent.ShouldContain("NewChannel");
    }

    [Fact]
    public async Task DoesNotAddDuplicateChannel()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "ruv1" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DoesNotAddEmptyOrWhitespaceChannel()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "   " });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(0);
    }

    [Fact]
    public async Task RemovesChannelWhenRemoveButtonClicked()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1", "RUV2"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement removeButton = cut.Find("button[aria-label='Remove RUV1']");
        await removeButton.ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
        tags[0].TextContent.ShouldContain("RUV2");
    }

    [Fact]
    public async Task ShowsDuplicateWarningWhenAddingExistingChannel()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "ruv1" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find(".field-error[role='alert']").TextContent.ShouldContain("Channel already in list");
    }

    [Fact]
    public async Task ClearsDuplicateWarningOnInputFocus()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "ruv1" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Act
        await input.FocusAsync(new FocusEventArgs());

        // Assert
        cut.FindAll(".field-error[role='alert']")
            .ShouldAllBe(e => !e.TextContent.Contains("Channel already in list"));
    }

    [Fact]
    public async Task ShowsConfirmationWhenSavingWithNewlyIgnoredChannels()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "NewChannel" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Act
        await cut.Find("button.save-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find(".save-warning[role='alert']").TextContent
            .ShouldContain("permanently deleted");
        cut.Find("button.save-button--danger").TextContent
            .ShouldContain("Confirm Save");
    }

    [Fact]
    public async Task CancelSaveHidesConfirmation()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement input = cut.Find("input[aria-label='Channel name']");
        await input.InputAsync(new ChangeEventArgs { Value = "NewChannel" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());
        await cut.Find("button.save-button").ClickAsync(new MouseEventArgs());

        // Act
        await cut.Find("button.cancel-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.FindAll(".save-warning").Count.ShouldBe(0);
        cut.Find("button.save-button").TextContent.ShouldContain("Save Settings");
    }

    [Fact]
    public async Task SavesDirectlyWhenNoNewlyIgnoredChannels()
    {
        // Arrange
        IRequestHandler<SaveSettingsCommand> saveHandler = RegisterSaveSettingsHandler();
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"] });
        RegisterTestConnectionHandler();

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        await cut.Find("button.save-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.FindAll(".save-warning").Count.ShouldBe(0);
        await saveHandler.Received(1).Handle(Arg.Any<SaveSettingsCommand>(), Arg.Any<CancellationToken>());
    }

    private void RegisterGetSettingsHandler(RuvarrSettings settings)
    {
        IRequestHandler<GetSettingsQuery, Result<RuvarrSettings>> handler =
            Substitute.For<IRequestHandler<GetSettingsQuery, Result<RuvarrSettings>>>();
        handler.Handle(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new Result<RuvarrSettings>(settings));
        Services.AddSingleton(handler);
    }

    private IRequestHandler<SaveSettingsCommand> RegisterSaveSettingsHandler()
    {
        IRequestHandler<SaveSettingsCommand> handler =
            Substitute.For<IRequestHandler<SaveSettingsCommand>>();
        handler.Handle(Arg.Any<SaveSettingsCommand>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddSingleton(handler);
        return handler;
    }

    private void RegisterTestConnectionHandler()
    {
        IRequestHandler<TestSonarrConnectionQuery> handler =
            Substitute.For<IRequestHandler<TestSonarrConnectionQuery>>();
        handler.Handle(Arg.Any<TestSonarrConnectionQuery>(), Arg.Any<CancellationToken>())
            .Returns(RuvarrResult.Success);
        Services.AddSingleton(handler);
    }
}
