using Ruvarr.Programs.Components;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.SeasonSelectorTests;

public sealed class SeasonSelectionRules
{
    [Fact]
    public void StartsFromTheSavedManualSeasons()
    {
        // Act
        SeasonSelection sut = new([2, 1]);

        // Assert
        sut.IsAutomatic.ShouldBeFalse();
        sut.Seasons.ShouldBe([1, 2]);
        sut.IsUnchanged.ShouldBeTrue();
    }

    [Fact]
    public void StartsAutomaticWithoutSavedSeasons()
    {
        // Act
        SeasonSelection sut = new([]);

        // Assert
        sut.IsAutomatic.ShouldBeTrue();
        sut.Seasons.ShouldBeEmpty();
    }

    [Fact]
    public void CheckingASeasonUnchecksAutomatic()
    {
        // Arrange
        SeasonSelection sut = new([]);

        // Act
        string? announcement = sut.ToggleSeason(2);

        // Assert
        sut.IsAutomatic.ShouldBeFalse();
        sut.Seasons.ShouldBe([2]);
        announcement.ShouldBe("Automatic turned off");
    }

    [Fact]
    public void CheckingAutomaticUnchecksEverySeason()
    {
        // Arrange
        SeasonSelection sut = new([1, 2]);

        // Act
        string? announcement = sut.ToggleAutomatic();

        // Assert
        sut.IsAutomatic.ShouldBeTrue();
        sut.Seasons.ShouldBeEmpty();
        announcement.ShouldBe("Seasons cleared");
    }

    [Fact]
    public void UncheckingTheLastSeasonRechecksAutomatic()
    {
        // Arrange
        SeasonSelection sut = new([2]);

        // Act
        string? announcement = sut.ToggleSeason(2);

        // Assert
        sut.IsAutomatic.ShouldBeTrue();
        sut.Seasons.ShouldBeEmpty();
        announcement.ShouldBe("Automatic turned on");
    }

    [Fact]
    public void UncheckingAutomaticAloneKeepsItChecked()
    {
        // Arrange
        SeasonSelection sut = new([]);

        // Act
        string? announcement = sut.ToggleAutomatic();

        // Assert
        sut.IsAutomatic.ShouldBeTrue();
        announcement.ShouldBeNull();
    }

    [Fact]
    public void UncheckingOneOfSeveralSeasonsKeepsTheRest()
    {
        // Arrange
        SeasonSelection sut = new([1, 2]);

        // Act
        string? announcement = sut.ToggleSeason(1);

        // Assert
        sut.Seasons.ShouldBe([2]);
        sut.IsAutomatic.ShouldBeFalse();
        announcement.ShouldBeNull();
    }

    [Fact]
    public void IsUnchangedWhenTheStagedSelectionEqualsTheSavedOne()
    {
        // Arrange
        SeasonSelection sut = new([1]);

        // Act
        sut.ToggleSeason(2);
        sut.ToggleSeason(2);

        // Assert
        sut.IsUnchanged.ShouldBeTrue();
    }

    [Fact]
    public void IsChangedAfterSwitchingToAutomatic()
    {
        // Arrange
        SeasonSelection sut = new([1]);

        // Act
        sut.ToggleAutomatic();

        // Assert
        sut.IsUnchanged.ShouldBeFalse();
    }
}
