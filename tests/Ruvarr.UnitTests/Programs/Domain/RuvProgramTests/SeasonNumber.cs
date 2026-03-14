using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class SeasonNumber
{
    [Fact]
    public void ReturnsSeasonFromRomanNumeralSuffix()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá II").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public void ReturnsSeasonFromIntegerSuffix()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá 2").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public void ReturnsZeroWhenNoSuffix()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(0);
    }

    [Fact]
    public void ReturnsZeroWhenSuffixIsZero()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá 0").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(0);
    }

    [Fact]
    public void ReturnsRomanNumeralBeforeInteger()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá I").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(1);
    }

    [Fact]
    public void ReturnsZeroWhenSuffixIsNonNumeric()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá ABC").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(0);
    }

    [Fact]
    public void AmbiguousSingleLetterParsedAsRomanNumeral()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder().WithName("Dagskrá V").Build();

        // Assert
        sut.SeasonNumber.ShouldBe(5);
    }
}
