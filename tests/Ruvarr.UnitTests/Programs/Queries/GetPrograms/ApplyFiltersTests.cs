using Ruvarr.Programs.Domain;
using Ruvarr.Programs.Queries.GetPrograms;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Queries.GetPrograms;

public sealed class ApplyFiltersTests
{
    private static readonly RuvProgram UnmatchedProgram = RuvProgram.Create(1, "RUV1", "Unmatched", null, multipleEpisodes: true);
    private static readonly RuvProgram SingleEpisodeProgram = RuvProgram.Create(2, "RUV2", "Single", null, multipleEpisodes: false);
    private static readonly RuvProgram WithForeignName = RuvProgram.Create(3, "RUV1", "Foreign", "English Name", multipleEpisodes: true);
    private static readonly RuvProgram WithoutForeignName = RuvProgram.Create(4, "RUV1", "No Foreign", null, multipleEpisodes: true);

    private static RuvProgram CreateMatchedSeriesProgram()
    {
        RuvProgram program = RuvProgram.Create(10, "RUV1", "Matched Series", null, multipleEpisodes: true);
        program.MatchTvdb(TvdbSeries.Create(100, "Series Name"));
        return program;
    }

    private static RuvProgram CreateMatchedMovieProgram()
    {
        RuvProgram program = RuvProgram.Create(11, "RUV1", "Matched Movie", null, multipleEpisodes: true);
        program.MatchTmdb(TmdbMovie.Create(200, "Movie Name"));
        return program;
    }

    private static RuvProgram CreateMonitoredProgram()
    {
        RuvProgram program = RuvProgram.Create(20, "RUV1", "Monitored", null, multipleEpisodes: true);
        program.SetMonitoredStatus(true);
        return program;
    }

    private static RuvProgram CreateNotMonitoredProgram()
    {
        return RuvProgram.Create(21, "RUV1", "Not Monitored", null, multipleEpisodes: true);
    }

    private static RuvProgram CreateMissingProgram()
    {
        RuvProgram program = RuvProgram.Create(30, "RUV1", "Missing", null, multipleEpisodes: true);
        program.SetHasMissingEpisodes(true);
        return program;
    }

    private static RuvProgram CreateNotMissingProgram()
    {
        return RuvProgram.Create(31, "RUV1", "Not Missing", null, multipleEpisodes: true);
    }

    private static RuvProgram CreatePendingLookupProgram()
    {
        RuvProgram program = RuvProgram.Create(40, "RUV1", "Pending Lookup", null, multipleEpisodes: true);
        program.ScheduleLookup();
        return program;
    }

    private static RuvProgram CreateNotPendingLookupProgram()
    {
        return RuvProgram.Create(41, "RUV1", "Not Pending", null, multipleEpisodes: true);
    }

    [Fact]
    public void NoFilters_ReturnsAllPrograms()
    {
        // Arrange
        IQueryable<RuvProgram> source = new[] { UnmatchedProgram, SingleEpisodeProgram }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void FiltersByChannel()
    {
        // Arrange
        IQueryable<RuvProgram> source = new[] { UnmatchedProgram, SingleEpisodeProgram }.AsQueryable();
        GetProgramsQuery query = new(Channel: "RUV1", null, null, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldAllBe(p => p.Channel == "RUV1");
        result.ShouldNotContain(p => p.Channel == "RUV2");
    }

    [Fact]
    public void FiltersByUnmatched_True()
    {
        // Arrange
        RuvProgram matched = CreateMatchedSeriesProgram();
        IQueryable<RuvProgram> source = new[] { UnmatchedProgram, matched }.AsQueryable();
        GetProgramsQuery query = new(null, IsUnmatched: true, null, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 1);
        result.ShouldNotContain(p => p.RuvId == 10);
    }

    [Fact]
    public void FiltersByUnmatched_False_IncludesSeriesAndMovies()
    {
        // Arrange
        RuvProgram matchedSeries = CreateMatchedSeriesProgram();
        RuvProgram matchedMovie = CreateMatchedMovieProgram();
        IQueryable<RuvProgram> source = new[] { UnmatchedProgram, matchedSeries, matchedMovie }.AsQueryable();
        GetProgramsQuery query = new(null, IsUnmatched: false, null, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 10);
        result.ShouldContain(p => p.RuvId == 11);
        result.ShouldNotContain(p => p.RuvId == 1);
    }

    [Fact]
    public void FiltersByMonitored_True()
    {
        // Arrange
        RuvProgram monitored = CreateMonitoredProgram();
        RuvProgram notMonitored = CreateNotMonitoredProgram();
        IQueryable<RuvProgram> source = new[] { monitored, notMonitored }.AsQueryable();
        GetProgramsQuery query = new(null, null, IsMonitored: true, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 20);
        result.ShouldNotContain(p => p.RuvId == 21);
    }

    [Fact]
    public void FiltersByMonitored_False()
    {
        // Arrange
        RuvProgram monitored = CreateMonitoredProgram();
        RuvProgram notMonitored = CreateNotMonitoredProgram();
        IQueryable<RuvProgram> source = new[] { monitored, notMonitored }.AsQueryable();
        GetProgramsQuery query = new(null, null, IsMonitored: false, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 21);
        result.ShouldNotContain(p => p.RuvId == 20);
    }

    [Fact]
    public void FiltersByMissing_True()
    {
        // Arrange
        RuvProgram missing = CreateMissingProgram();
        RuvProgram notMissing = CreateNotMissingProgram();
        IQueryable<RuvProgram> source = new[] { missing, notMissing }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, IsMissing: true, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 30);
        result.ShouldNotContain(p => p.RuvId == 31);
    }

    [Fact]
    public void FiltersByMissing_False()
    {
        // Arrange
        RuvProgram missing = CreateMissingProgram();
        RuvProgram notMissing = CreateNotMissingProgram();
        IQueryable<RuvProgram> source = new[] { missing, notMissing }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, IsMissing: false, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 31);
        result.ShouldNotContain(p => p.RuvId == 30);
    }

    [Fact]
    public void FiltersByPendingLookup_True()
    {
        // Arrange
        RuvProgram pending = CreatePendingLookupProgram();
        RuvProgram notPending = CreateNotPendingLookupProgram();
        IQueryable<RuvProgram> source = new[] { pending, notPending }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, null, IsPendingLookup: true, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 40);
        result.ShouldNotContain(p => p.RuvId == 41);
    }

    [Fact]
    public void FiltersByPendingLookup_False()
    {
        // Arrange
        RuvProgram pending = CreatePendingLookupProgram();
        RuvProgram notPending = CreateNotPendingLookupProgram();
        IQueryable<RuvProgram> source = new[] { pending, notPending }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, null, IsPendingLookup: false, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 41);
        result.ShouldNotContain(p => p.RuvId == 40);
    }

    [Fact]
    public void FiltersByHasForeignName_True()
    {
        // Arrange
        IQueryable<RuvProgram> source = new[] { WithForeignName, WithoutForeignName }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, null, null, HasForeignName: true, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 3);
        result.ShouldNotContain(p => p.RuvId == 4);
    }

    [Fact]
    public void FiltersByHasForeignName_False()
    {
        // Arrange
        IQueryable<RuvProgram> source = new[] { WithForeignName, WithoutForeignName }.AsQueryable();
        GetProgramsQuery query = new(null, null, null, null, null, HasForeignName: false, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 4);
        result.ShouldNotContain(p => p.RuvId == 3);
    }

    [Fact]
    public void CombinesMultipleFilters()
    {
        // Arrange
        RuvProgram monitored = CreateMonitoredProgram();
        RuvProgram notMonitored = CreateNotMonitoredProgram();
        IQueryable<RuvProgram> source = new[] { UnmatchedProgram, monitored, notMonitored }.AsQueryable();
        GetProgramsQuery query = new(Channel: "RUV1", null, IsMonitored: true, null, null, null, null);

        // Act
        List<RuvProgram> result = source.ApplyFilters(query).ToList();

        // Assert
        result.ShouldContain(p => p.RuvId == 20);
        result.ShouldNotContain(p => p.RuvId == 21);
        result.ShouldNotContain(p => p.RuvId == 1);
    }
}
