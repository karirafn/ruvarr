using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.SeasonSelectorTests;

public sealed class TriggerLabel
{
    [Fact]
    public void Automatic_ShowsTheParsedSeason()
    {
        // Act & Assert
        SeasonSelector.FormatTriggerLabel([], parsedSeasonNumber: 2).ShouldBe("Season: Auto (2)");
    }

    [Fact]
    public void Automatic_WithoutAParsedSeason()
    {
        // Act & Assert
        SeasonSelector.FormatTriggerLabel([], parsedSeasonNumber: null).ShouldBe("Season: Auto");
    }

    [Fact]
    public void Manual_ListsTheSeasons()
    {
        // Act & Assert
        SeasonSelector.FormatTriggerLabel([1, 2], parsedSeasonNumber: 2).ShouldBe("Season: 1, 2");
    }

    [Fact]
    public void Manual_CountsTheSeasonsAfterTheThird()
    {
        // Act & Assert
        SeasonSelector.FormatTriggerLabel([1, 2, 3, 4, 5], parsedSeasonNumber: null).ShouldBe("Season: 1, 2, 3 +2");
    }

    [Fact]
    public void FullLabel_ListsEverySeason()
    {
        // Act & Assert
        SeasonSelector.FormatFullLabel([1, 2, 3, 4, 5], parsedSeasonNumber: null).ShouldBe("Season: 1, 2, 3, 4, 5");
    }

    [Theory]
    [InlineData("II", 2, "Automatic (II → 2)")]
    [InlineData("3", 3, "Automatic (3)")]
    [InlineData(null, null, "Automatic")]
    public void AutomaticRow_ShowsWhereTheSeasonComesFrom(string? token, int? number, string expected)
    {
        // Act & Assert
        SeasonSelector.FormatAutomaticLabel(token, number).ShouldBe(expected);
    }
}
