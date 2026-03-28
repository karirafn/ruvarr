using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Settings;
using Ruvarr.Settings.Commands.SaveSettings;
using Ruvarr.Settings.Queries.GetDistinctChannels;
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
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1", "RUV2"], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1", "RUV2", "RUV3"]);

        // Act
        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(2);
        tags[0].TextContent.ShouldContain("RUV1");
        tags[1].TextContent.ShouldContain("RUV2");
    }

    [Fact]
    public async Task AddsChannelViaDropdownAndButton()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["ChannelA", "ChannelB"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement select = cut.Find("select[aria-label='Available channels']");
        await select.ChangeAsync(new ChangeEventArgs { Value = "ChannelA" });
        await cut.Find("button.add-channel-button").ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
        tags[0].TextContent.ShouldContain("ChannelA");

        IReadOnlyList<IElement> options = cut.FindAll("select[aria-label='Available channels'] option:not([disabled])");
        options.Count.ShouldBe(1);
        options[0].TextContent.ShouldContain("ChannelB");
    }

    [Fact]
    public async Task RemovesChannelWhenRemoveButtonClicked()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1", "RUV2"], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1", "RUV2", "RUV3"]);

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
    public void DisablesDropdownWhenAllChannelsIgnored()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1", "RUV2"], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1", "RUV2"]);

        // Act
        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Assert
        IElement select = cut.Find("select[aria-label='Available channels']");
        select.HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task RemovedChannelReappearsInDropdown()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1", "RUV2"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement removeButton = cut.Find("button[aria-label='Remove RUV1']");
        await removeButton.ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> options = cut.FindAll("select[aria-label='Available channels'] option:not([disabled])");
        options.Count.ShouldBe(2);
        options.ShouldContain(o => o.TextContent.Contains("RUV1"));
        options.ShouldContain(o => o.TextContent.Contains("RUV2"));
    }

    [Fact]
    public async Task RemovedChannelReappearsInDropdownEvenWhenNotInDatabase()
    {
        // Arrange — ignored channel "RUV Aukaras" has no programs in the DB,
        // so GetDistinctChannels does not return it
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV Aukaras", "RUV2"], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act — remove "RUV Aukaras" from the ignored list
        IElement removeButton = cut.Find("button[aria-label='Remove RUV Aukaras']");
        await removeButton.ClickAsync(new MouseEventArgs());

        // Assert — "RUV Aukaras" should appear in the dropdown alongside "RUV1"
        IReadOnlyList<IElement> options = cut.FindAll("select[aria-label='Available channels'] option:not([disabled])");
        options.Count.ShouldBe(2);
        options.ShouldContain(o => o.TextContent.Contains("RUV Aukaras"));
        options.ShouldContain(o => o.TextContent.Contains("RUV1"));

        // "RUV2" is still ignored, so it should not be in the dropdown
        options.ShouldNotContain(o => o.TextContent.Contains("RUV2"));
    }

    [Fact]
    public void RendersExistingIgnoredProgramsAsTags()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = ["Fréttir", "Kastljós"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler([]);

        // Act
        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(2);
        tags[0].TextContent.ShouldContain("Fréttir");
        tags[1].TextContent.ShouldContain("Kastljós");
    }

    [Fact]
    public async Task AddsProgramViaTextInputAndButton()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler([]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Program title to ignore']");
        await input.InputAsync(new ChangeEventArgs { Value = "New Program" });
        await cut.FindAll("button.add-channel-button")[1].ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
        tags[0].TextContent.ShouldContain("New Program");
    }

    [Fact]
    public async Task RemovesProgramWhenRemoveButtonClicked()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = ["Fréttir", "Kastljós"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler([]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement removeButton = cut.Find("button[aria-label='Remove Fréttir']");
        await removeButton.ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
        tags[0].TextContent.ShouldContain("Kastljós");
    }

    [Fact]
    public async Task DoesNotAddDuplicateProgramCaseInsensitive()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = ["Fréttir"] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler([]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        // Act
        IElement input = cut.Find("input[aria-label='Program title to ignore']");
        await input.InputAsync(new ChangeEventArgs { Value = "fréttir" });
        await cut.FindAll("button.add-channel-button")[1].ClickAsync(new MouseEventArgs());

        // Assert
        IReadOnlyList<IElement> tags = cut.FindAll("[role='listitem']");
        tags.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ShowsConfirmationWhenSavingWithNewlyIgnoredPrograms()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler([]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement input = cut.Find("input[aria-label='Program title to ignore']");
        await input.InputAsync(new ChangeEventArgs { Value = "New Show" });
        await cut.FindAll("button.add-channel-button")[1].ClickAsync(new MouseEventArgs());

        // Act
        await cut.Find("button.save-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find(".save-warning[role='alert']").TextContent
            .ShouldContain("Matching programs will be permanently deleted.");
    }

    [Fact]
    public async Task ShowsCombinedWarningWhenBothChannelsAndProgramsNewlyIgnored()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["NewChannel"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement select = cut.Find("select[aria-label='Available channels']");
        await select.ChangeAsync(new ChangeEventArgs { Value = "NewChannel" });
        await cut.FindAll("button.add-channel-button")[0].ClickAsync(new MouseEventArgs());

        IElement input = cut.Find("input[aria-label='Program title to ignore']");
        await input.InputAsync(new ChangeEventArgs { Value = "New Show" });
        await cut.FindAll("button.add-channel-button")[1].ClickAsync(new MouseEventArgs());

        // Act
        await cut.Find("button.save-button").ClickAsync(new MouseEventArgs());

        // Assert
        cut.Find(".save-warning[role='alert']").TextContent
            .ShouldContain("Programs for newly ignored channels and matching ignored programs will be permanently deleted.");
    }

    [Fact]
    public async Task ShowsConfirmationWhenSavingWithNewlyIgnoredChannels()
    {
        // Arrange
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["NewChannel"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement select = cut.Find("select[aria-label='Available channels']");
        await select.ChangeAsync(new ChangeEventArgs { Value = "NewChannel" });
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
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = [], IgnoredPrograms = [] });
        RegisterSaveSettingsHandler();
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["NewChannel"]);

        IRenderedComponent<Ruvarr.Settings.Settings> cut = Render<Ruvarr.Settings.Settings>();

        IElement select = cut.Find("select[aria-label='Available channels']");
        await select.ChangeAsync(new ChangeEventArgs { Value = "NewChannel" });
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
        RegisterGetSettingsHandler(new RuvarrSettings { IgnoredChannels = ["RUV1"], IgnoredPrograms = [] });
        RegisterTestConnectionHandler();
        RegisterGetDistinctChannelsHandler(["RUV1", "RUV2"]);

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

        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(settings);
        Services.AddSingleton(settingsStore);
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

    private void RegisterGetDistinctChannelsHandler(List<string> channels)
    {
        IRequestHandler<GetDistinctChannelsQuery, List<string>> handler =
            Substitute.For<IRequestHandler<GetDistinctChannelsQuery, List<string>>>();
        handler.Handle(Arg.Any<GetDistinctChannelsQuery>(), Arg.Any<CancellationToken>())
            .Returns(channels);
        Services.AddSingleton(handler);
    }
}
