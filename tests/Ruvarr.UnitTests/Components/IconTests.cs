using AngleSharp.Dom;

using Bunit;

using Ruvarr.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Components;

public sealed class IconTests : BunitContext
{
    [Theory]
    [InlineData(IconType.BackArrow)]
    [InlineData(IconType.Checkmark)]
    [InlineData(IconType.CheckmarkCircle)]
    [InlineData(IconType.Clock)]
    [InlineData(IconType.Copy)]
    [InlineData(IconType.Download)]
    [InlineData(IconType.ExternalLink)]
    [InlineData(IconType.Eye)]
    [InlineData(IconType.EyeOff)]
    [InlineData(IconType.Link)]
    [InlineData(IconType.LinkDiagonal)]
    [InlineData(IconType.Refresh)]
    [InlineData(IconType.Spinner)]
    [InlineData(IconType.Warning)]
    public void Renders_Svg_For_Each_IconType(IconType type)
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, type));

        IElement svg = cut.Find("svg");
        svg.ShouldNotBeNull();
        svg.GetAttribute("xmlns").ShouldBe("http://www.w3.org/2000/svg");
        svg.GetAttribute("viewBox").ShouldBe("0 0 24 24");
        svg.GetAttribute("fill").ShouldBe("none");
        svg.GetAttribute("stroke").ShouldBe("currentColor");
        svg.GetAttribute("stroke-width").ShouldBe("2");
        svg.GetAttribute("stroke-linecap").ShouldBe("round");
        svg.GetAttribute("stroke-linejoin").ShouldBe("round");
        svg.InnerHtml.ShouldNotBeEmpty();
    }

    [Fact]
    public void Renders_Class_Attribute_When_Specified()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Checkmark)
            .Add(p => p.Class, "icon icon--matched-all"));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("class").ShouldBe("icon icon--matched-all");
    }

    [Fact]
    public void Does_Not_Render_Class_Attribute_When_Null()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Checkmark));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("class").ShouldBeNull();
    }

    [Fact]
    public void Renders_AriaLabel_When_Specified()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Download)
            .Add(p => p.AriaLabel, "Download episode"));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("aria-label").ShouldBe("Download episode");
        svg.GetAttribute("aria-hidden").ShouldBeNull();
    }

    [Fact]
    public void Renders_AriaHidden_When_AriaLabel_Is_Null()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.BackArrow));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("aria-hidden").ShouldBe("true");
        svg.GetAttribute("aria-label").ShouldBeNull();
    }

    [Fact]
    public void Renders_Custom_Width_And_Height()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.BackArrow)
            .Add(p => p.Width, "14")
            .Add(p => p.Height, "14"));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("width").ShouldBe("14");
        svg.GetAttribute("height").ShouldBe("14");
    }

    [Fact]
    public void Does_Not_Render_Width_And_Height_When_Null()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Checkmark));

        IElement svg = cut.Find("svg");
        svg.GetAttribute("width").ShouldBeNull();
        svg.GetAttribute("height").ShouldBeNull();
    }

    [Fact]
    public void BackArrow_Renders_Correct_Paths()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.BackArrow));

        cut.Find("svg").InnerHtml.ShouldContain("M19 12H5");
        cut.Find("svg").InnerHtml.ShouldContain("m12 19-7-7 7-7");
    }

    [Fact]
    public void Checkmark_Renders_Correct_Polyline()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Checkmark));

        cut.Find("svg polyline").GetAttribute("points").ShouldBe("20 6 9 17 4 12");
    }

    [Fact]
    public void Clock_Renders_Circle_And_Polyline()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Clock));

        cut.Find("svg circle").GetAttribute("cx").ShouldBe("12");
        cut.Find("svg polyline").GetAttribute("points").ShouldBe("12 6 12 12 16 14");
    }

    [Fact]
    public void Spinner_Renders_Correct_Path()
    {
        IRenderedComponent<Icon> cut = Render<Icon>(parameters => parameters
            .Add(p => p.Type, IconType.Spinner));

        cut.Find("svg").InnerHtml.ShouldContain("M21 12a9 9 0 1 1-6.219-8.56");
    }
}
