using AngleSharp.Dom;

using Bunit;

using Ruvarr.SonarrFolderCheck;
using Ruvarr.SonarrFolderCheck.Components;

using Shouldly;

namespace Ruvarr.UnitTests.SonarrFolderCheck.Components;

public sealed class SonarrFolderCheckWarningTests : BunitContext
{
    private const string Folder = "/data/torrents/tv";
    private static readonly DateTimeOffset CheckedAt = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private IRenderedComponent<SonarrFolderCheckWarning> RenderWith(SonarrFolderCheckResult? check) =>
        Render<SonarrFolderCheckWarning>(p => p.Add(x => x.Check, check));

    [Fact]
    public void AlwaysRendersPoliteLiveRegion()
    {
        // Act
        IRenderedComponent<SonarrFolderCheckWarning> cut = RenderWith(null);

        // Assert
        IElement region = cut.Find(".folder-check-warning");
        region.GetAttribute("role").ShouldBe("status");
        region.GetAttribute("aria-atomic").ShouldBe("true");
        region.TextContent.Trim().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SonarrFolderCheckStatus.Passing)]
    [InlineData(SonarrFolderCheckStatus.Unknown)]
    public void RendersNoWarning_UnlessFailing(SonarrFolderCheckStatus status)
    {
        // Act
        IRenderedComponent<SonarrFolderCheckWarning> cut = RenderWith(new(status, Folder, CheckedAt));

        // Assert
        cut.Find(".folder-check-warning").TextContent.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void RendersCautionStripNamingTheProbedFolder_WhenFailing()
    {
        // Act
        IRenderedComponent<SonarrFolderCheckWarning> cut = RenderWith(new(SonarrFolderCheckStatus.Failing, Folder, CheckedAt));

        // Assert
        IElement strip = cut.Find(".folder-check-warning .caution-strip.folder-check-warning__strip");
        strip.QuerySelector(".folder-check-warning__headline")!.TextContent.ShouldBe("Warning: Sonarr can't see Ruvarr's download folder");
        strip.QuerySelector(".folder-check-warning__headline .visually-hidden")!.TextContent.ShouldBe("Warning: ");
        strip.QuerySelector("code.folder-check-warning__path")!.TextContent.ShouldBe(Folder);
        strip.TextContent.ShouldContain("Mount the downloads share at the same container path in both Ruvarr and Sonarr.");
        strip.TextContent.ShouldContain("Downloads that failed for this reason retry automatically once the check passes.");
        strip.QuerySelector("time")!.GetAttribute("datetime").ShouldBe(CheckedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture));
        strip.QuerySelector(".folder-check-warning__meta")!.TextContent.ShouldContain("rechecked every 10 minutes");
    }
}
