using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;

using NSubstitute;

using Ruvarr.Components.Layout;
using Ruvarr.Settings;

using Shouldly;

namespace Ruvarr.UnitTests.Components.Layout;

public sealed class MainLayoutTests : BunitContext
{
    [Fact]
    public void RendersMobileTopBarAndHamburgerButton()
    {
        // Arrange
        RegisterSettingsStore();

        // Act
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();

        // Assert
        IElement topbar = cut.Find("header.mobile-topbar");
        topbar.ShouldNotBeNull();
        topbar.QuerySelector(".mobile-topbar__title")!.TextContent.ShouldBe("Ruvarr");

        IElement button = topbar.QuerySelector("button.hamburger-button")!;
        button.ShouldNotBeNull();
        button.GetAttribute("aria-label").ShouldBe("Toggle navigation");
    }

    [Fact]
    public void DrawerStartsClosed()
    {
        // Arrange
        RegisterSettingsStore();

        // Act
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();

        // Assert
        IElement layoutDiv = cut.Find("div.layout");
        layoutDiv.ClassList.ShouldNotContain("layout--drawer-open");
    }

    [Fact]
    public void ClickingHamburgerOpensDrawer()
    {
        // Arrange
        RegisterSettingsStore();
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();

        // Act
        cut.Find("button.hamburger-button").Click();

        // Assert
        IElement layoutDiv = cut.Find("div.layout");
        layoutDiv.ClassList.ShouldContain("layout--drawer-open");
    }

    [Fact]
    public void ClickingBackdropClosesDrawer()
    {
        // Arrange
        RegisterSettingsStore();
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();
        cut.Find("button.hamburger-button").Click();

        // Act
        cut.Find("button.nav-backdrop").Click();

        // Assert
        IElement layoutDiv = cut.Find("div.layout");
        layoutDiv.ClassList.ShouldNotContain("layout--drawer-open");
    }

    [Fact]
    public void NavigationClosesDrawer()
    {
        // Arrange
        RegisterSettingsStore();
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();
        cut.Find("button.hamburger-button").Click();
        cut.Find("div.layout").ClassList.ShouldContain("layout--drawer-open");

        // Act
        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/programs");

        // Assert
        IElement layoutDiv = cut.Find("div.layout");
        layoutDiv.ClassList.ShouldNotContain("layout--drawer-open");
    }

    [Fact]
    public void EscapeKeyClosesDrawer()
    {
        // Arrange
        RegisterSettingsStore();
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();
        cut.Find("button.hamburger-button").Click();
        cut.Find("div.layout").ClassList.ShouldContain("layout--drawer-open");

        // Act
        cut.Find("div.layout").KeyDown(Key.Escape);

        // Assert
        IElement layoutDiv = cut.Find("div.layout");
        layoutDiv.ClassList.ShouldNotContain("layout--drawer-open");
    }

    [Fact]
    public void AriaExpandedReflectsDrawerState()
    {
        // Arrange
        RegisterSettingsStore();
        IRenderedComponent<MainLayout> cut = Render<MainLayout>();

        // Assert — closed
        IElement button = cut.Find("button.hamburger-button");
        button.GetAttribute("aria-expanded").ShouldBe("false");

        // Act — open
        button.Click();

        // Assert — open
        button = cut.Find("button.hamburger-button");
        button.GetAttribute("aria-expanded").ShouldBe("true");
    }

    private void RegisterSettingsStore()
    {
        ISettingsStore settingsStore = Substitute.For<ISettingsStore>();
        settingsStore.Current.Returns(new RuvarrSettings(
            SonarrBaseAddress: "http://localhost",
            SonarrApiKey: "key",
            TvdbApiKey: "key",
            TmdbApiKey: "key"));
        Services.AddSingleton(settingsStore);
    }
}
