using System.Text;

using Shouldly;

using ProgramsPage = Ruvarr.Programs.Programs;

namespace Ruvarr.UnitTests.Programs.ProgramsPageTests;

public sealed class MatchesSearch
{
    [Fact]
    public void WhenSearchTextIsSubstring_ReturnsTrue()
    {
        // Arrange
        string programName = "Frettir";
        string searchText = "rett";

        // Act
        bool result = ProgramsPage.MatchesSearch(programName, searchText);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenSearchTextDiffersByCase_ReturnsTrue()
    {
        // Arrange
        string programName = "Frettir";
        string searchText = "FRETTIR";

        // Act
        bool result = ProgramsPage.MatchesSearch(programName, searchText);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenSearchTextIsNotSubstring_ReturnsFalse()
    {
        // Arrange
        string programName = "Frettir";
        string searchText = "Spurningar";

        // Act
        bool result = ProgramsPage.MatchesSearch(programName, searchText);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public void WhenSearchTextIsNfd_MatchesNfcProgramName()
    {
        // Arrange
        // NFC program name: composed o-umlaut \u00F6, a-acute \u00E1
        string programName = "Skjaldb\u00f6kustr\u00e1kur";
        // NFD search text: o + \u0308 (COMBINING DIAERESIS), a + \u0301 (COMBINING ACUTE ACCENT)
        // Written with explicit \u escapes so git/editor normalization cannot collapse them.
        string searchText = "skjaldb\u006f\u0308kustr\u0061\u0301kur";

        // Act
        bool result = ProgramsPage.MatchesSearch(programName, searchText);

        // Assert
        searchText.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();
        result.ShouldBeTrue();
    }

    [Fact]
    public void WhenProgramNameIsNfd_MatchesNfcSearchText()
    {
        // Arrange
        // NFD program name: o + \u0308 (COMBINING DIAERESIS), a + \u0301 (COMBINING ACUTE ACCENT)
        // Written with explicit \u escapes so git/editor normalization cannot collapse them.
        string programName = "Skjaldb\u006f\u0308kustr\u0061\u0301kur";
        // NFC search text: composed o-umlaut \u00F6, a-acute \u00E1
        string searchText = "Skjaldb\u00f6kustr\u00e1kur";

        // Act
        bool result = ProgramsPage.MatchesSearch(programName, searchText);

        // Assert
        programName.IsNormalized(NormalizationForm.FormC).ShouldBeFalse();
        result.ShouldBeTrue();
    }
}
