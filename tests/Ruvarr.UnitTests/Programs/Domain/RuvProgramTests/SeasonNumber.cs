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

    [Fact]
    public void FallsBackToForeignNameRomanNumeral()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Lögregluvaktin")
            .WithForeignName("Chicago PD IX")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(9);
    }

    [Fact]
    public void FallsBackToForeignNameInteger()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Lögregluvaktin")
            .WithForeignName("Chicago PD 9")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(9);
    }

    [Fact]
    public void ForeignNameRomanNumeralX()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Séra Brown")
            .WithForeignName("Father Brown X")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(10);
    }

    [Fact]
    public void ForeignNameRomanNumeralIII()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Kúlugúbbarnir")
            .WithForeignName("Bubble Guppies III")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(3);
    }

    [Fact]
    public void NameTakesPrecedenceOverForeignName()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Dagskrá II")
            .WithForeignName("Show IX")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(2);
    }

    [Fact]
    public void ReturnsZeroWhenBothNamesLackSeason()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Dagskrá")
            .WithForeignName("Some Show")
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(0);
    }

    [Fact]
    public void ReturnsZeroWhenForeignNameIsNull()
    {
        // Arrange / Act
        RuvProgram sut = new RuvProgramBuilder()
            .WithName("Dagskrá")
            .WithForeignName(null)
            .Build();

        // Assert
        sut.SeasonNumber.ShouldBe(0);
    }
}
