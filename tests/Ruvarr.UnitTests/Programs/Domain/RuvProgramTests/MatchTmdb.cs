using Ruvarr.Programs.Domain;
using Ruvarr.UnitTests.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class MatchTmdb
{
    [Fact]
    public void SetsMovie()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        TmdbMovie movie = new TmdbMovieBuilder().Build();

        // Act
        sut.MatchTmdb(movie);

        // Assert
        sut.Movie.ShouldBe(movie);
    }

    [Fact]
    public void SetsMatchedToUtcNow()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        DateTime before = DateTime.UtcNow;

        // Act
        sut.MatchTmdb(new TmdbMovieBuilder().Build());

        // Assert
        sut.Matched.ShouldNotBeNull();
        sut.Matched.Value.ShouldBeInRange(before, DateTime.UtcNow);
    }

    [Fact]
    public void ClearsNextLookup()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();
        sut.ScheduleLookup();
        sut.NextLookup.ShouldNotBeNull();

        // Act
        sut.MatchTmdb(new TmdbMovieBuilder().Build());

        // Assert
        sut.NextLookup.ShouldBeNull();
    }

    [Fact]
    public void IncrementsLookupCount()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().Build();

        // Act
        sut.MatchTmdb(new TmdbMovieBuilder().Build());

        // Assert
        sut.LookupCount.ShouldBe(1);
    }
}
