using Ruvarr.Infrastructure.Tvdb.Models;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Domain.RuvProgramTests;

public sealed class ResolveMatchingSeason
{
    [Fact]
    public void SeasonZero_ExactlyOneSeasonMatchesEpisodeCount_ReturnsThatSeason()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        sut.TryAddEpisode("ep2", new Uri("http://test.com"), "þáttur 2", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode s1e1 = new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build();
        Episode s1e2 = new TvdbEpisodeDataBuilder().WithId(102).WithSeasonNumber(1).WithNumber(2).Build();
        Episode s2e1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode s2e2 = new TvdbEpisodeDataBuilder().WithId(202).WithSeasonNumber(2).WithNumber(2).Build();
        Episode s2e3 = new TvdbEpisodeDataBuilder().WithId(203).WithSeasonNumber(2).WithNumber(3).Build();

        // Act
        int result = sut.ResolveMatchingSeason([s1e1, s1e2, s2e1, s2e2, s2e3]);

        // Assert
        result.ShouldBe(1);
    }

    [Fact]
    public void SeasonZero_NoSeasonMatchesEpisodeCount_ReturnsZero()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode s1e1 = new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build();
        Episode s1e2 = new TvdbEpisodeDataBuilder().WithId(102).WithSeasonNumber(1).WithNumber(2).Build();

        // Act
        int result = sut.ResolveMatchingSeason([s1e1, s1e2]);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void SeasonZero_TwoSeasonsMatchEpisodeCount_ReturnsZero()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode s1e1 = new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build();
        Episode s2e1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();

        // Act
        int result = sut.ResolveMatchingSeason([s1e1, s2e1]);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void SeasonNumbered_NoOtherSeasonSharesEpisodeCount_ReturnsSeasonNumber()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show II").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode s1e1 = new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build();
        Episode s1e2 = new TvdbEpisodeDataBuilder().WithId(102).WithSeasonNumber(1).WithNumber(2).Build();
        Episode s2e1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();

        // Act
        int result = sut.ResolveMatchingSeason([s1e1, s1e2, s2e1]);

        // Assert
        result.ShouldBe(2);
    }

    [Fact]
    public void SeasonNumbered_AnotherSeasonSharesEpisodeCount_ReturnsZero()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show II").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode s2e1 = new TvdbEpisodeDataBuilder().WithId(201).WithSeasonNumber(2).WithNumber(1).Build();
        Episode s4e1 = new TvdbEpisodeDataBuilder().WithId(401).WithSeasonNumber(4).WithNumber(1).Build();

        // Act
        int result = sut.ResolveMatchingSeason([s2e1, s4e1]);

        // Assert
        result.ShouldBe(0);
    }

    [Fact]
    public void Season0SpecialsAreIgnored()
    {
        // Arrange
        RuvProgram sut = new RuvProgramBuilder().WithName("Show").Build();
        sut.TryAddEpisode("ep1", new Uri("http://test.com"), "þáttur 1", "", DateTime.UtcNow, TimeSpan.FromMinutes(30));

        Episode special = new TvdbEpisodeDataBuilder().WithId(1).WithSeasonNumber(0).WithNumber(1).Build();
        Episode s1e1 = new TvdbEpisodeDataBuilder().WithId(101).WithSeasonNumber(1).WithNumber(1).Build();

        // Act
        int result = sut.ResolveMatchingSeason([special, s1e1]);

        // Assert
        result.ShouldBe(1);
    }
}
