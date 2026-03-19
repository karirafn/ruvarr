using Ruvarr.Programs.Domain;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvEpisodeTests;

public sealed class PartTitleParsing
{
    [Theory]
    [InlineData("Jólasveinar, síðari hluti")]
    [InlineData("Jólasveinar - síðari hluti")]
    [InlineData("Jólasveinar, Síðari hluti")]
    [InlineData("Jólasveinar - Síðari hluti")]
    public void IsPartTwo_ReturnsTrueForPartTwoTitles(string title)
    {
        // Act
        bool result = RuvEpisode.IsPartTwo(title);

        // Assert
        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Jólasveinar, fyrri hluti")]
    [InlineData("Jólasveinar - fyrri hluti")]
    [InlineData("Jólasveinar")]
    [InlineData("þáttur 1")]
    [InlineData("")]
    public void IsPartTwo_ReturnsFalseForNonPartTwoTitles(string title)
    {
        // Act
        bool result = RuvEpisode.IsPartTwo(title);

        // Assert
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("Jólasveinar, síðari hluti", "Jólasveinar")]
    [InlineData("Jólasveinar - síðari hluti", "Jólasveinar")]
    [InlineData("Draugar á Djúpalæk, síðari hluti", "Draugar á Djúpalæk")]
    [InlineData("Draugar á Djúpalæk - síðari hluti", "Draugar á Djúpalæk")]
    [InlineData("Jólasveinar, fyrri hluti", "Jólasveinar")]
    [InlineData("Jólasveinar - fyrri hluti", "Jólasveinar")]
    [InlineData("Jólasveinar", "Jólasveinar")]
    [InlineData("þáttur 1", "þáttur 1")]
    public void GetBaseTitle_ExtractsBaseTitleOrReturnsTitleUnchanged(string title, string expectedBase)
    {
        // Act
        string result = RuvEpisode.GetBaseTitle(title);

        // Assert
        result.ShouldBe(expectedBase);
    }

    [Theory]
    [InlineData("Jólasveinar, síðari hluti", "Jólasveinar, fyrri hluti")]
    [InlineData("Jólasveinar - síðari hluti", "Jólasveinar - fyrri hluti")]
    [InlineData("Jólasveinar, Síðari hluti", "Jólasveinar, Fyrri hluti")]
    [InlineData("Jólasveinar - Síðari hluti", "Jólasveinar - Fyrri hluti")]
    public void ToPartOneTitle_ConvertsPartTwoToPartOneTitle(string title, string expectedPartOne)
    {
        // Act
        string result = RuvEpisode.ToPartOneTitle(title);

        // Assert
        result.ShouldBe(expectedPartOne);
    }
}
