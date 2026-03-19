using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Sonarr.Models;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.AddProgramToSonarr;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.AddProgramToSonarr.AddProgramToSonarrHandlerTests;

public sealed class Handle
{
    private readonly ISonarrClient _sonarrClient = Substitute.For<ISonarrClient>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();

    public Handle()
    {
        _serviceProvider.GetService(Arg.Any<Type>()).Returns(Array.Empty<object>());
    }

    private RuvarrDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<RuvarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
        _serviceProvider);

    private AddProgramToSonarrHandler CreateHandler(RuvarrDbContext dbContext) => new(
        dbContext, _sonarrClient);

    [Fact]
    public async Task ReturnsProgramNotFoundWhenProgramDoesNotExist()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(999, 1, "/tv"), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
        await _sonarrClient.DidNotReceive().AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsProgramNotMatchedWhenProgramHasNoSeries()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(1, 1, "/tv"), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(ProgramErrors.ProgramNotMatched);
        await _sonarrClient.DidNotReceive().AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsInvalidRootFolderPathWhenPathNotInSonarr()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(12345).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _sonarrClient.GetRootFoldersAsync(Arg.Any<CancellationToken>())
            .Returns([new RootFolder(1, "/tv"), new RootFolder(2, "/media/shows")]);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(1, 1, "/invalid/path"), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SonarrErrors.InvalidRootFolderPath);
        await _sonarrClient.DidNotReceive().AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsInvalidQualityProfileIdWhenProfileNotInSonarr()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(12345).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _sonarrClient.GetRootFoldersAsync(Arg.Any<CancellationToken>())
            .Returns([new RootFolder(1, "/tv")]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<CancellationToken>())
            .Returns([new QualityProfile(1, "HD-1080p"), new QualityProfile(2, "SD")]);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(1, 999, "/tv"), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SonarrErrors.InvalidQualityProfileId);
        await _sonarrClient.DidNotReceive().AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReturnsAddSeriesFailedWhenSonarrReturnsNull()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(12345).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _sonarrClient.GetRootFoldersAsync(Arg.Any<CancellationToken>())
            .Returns([new RootFolder(1, "/tv")]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<CancellationToken>())
            .Returns([new QualityProfile(1, "HD-1080p")]);
        _sonarrClient.AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>())
            .Returns((Series?)null);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(1, 1, "/tv"), CancellationToken.None);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SonarrErrors.AddSeriesFailed);
    }

    [Fact]
    public async Task ReturnsSuccessWhenSonarrAddsSeriesSuccessfully()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Test Program").Build();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(12345).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _sonarrClient.GetRootFoldersAsync(Arg.Any<CancellationToken>())
            .Returns([new RootFolder(1, "/tv")]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<CancellationToken>())
            .Returns([new QualityProfile(1, "HD-1080p")]);
        Series sonarrSeries = CreateSonarrSeries(42, 12345);
        _sonarrClient.AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>())
            .Returns(sonarrSeries);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(
            new AddProgramToSonarrCommand(1, 1, "/tv"), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task SendsCorrectRequestToSonarrUsingEntityValues()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).WithName("Test Program").Build();
        TvdbSeries series = new TvdbSeriesBuilder().WithId(12345).Build();
        program.MatchTvdb(series);
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        _sonarrClient.GetRootFoldersAsync(Arg.Any<CancellationToken>())
            .Returns([new RootFolder(1, "/media/tv")]);
        _sonarrClient.GetQualityProfilesAsync(Arg.Any<CancellationToken>())
            .Returns([new QualityProfile(7, "HD-1080p")]);
        Series sonarrSeries = CreateSonarrSeries(42, 12345);
        _sonarrClient.AddSeriesAsync(Arg.Any<AddSeriesRequest>(), Arg.Any<CancellationToken>())
            .Returns(sonarrSeries);

        AddProgramToSonarrHandler sut = CreateHandler(dbContext);

        // Act
        await sut.Handle(
            new AddProgramToSonarrCommand(1, 7, "/media/tv"), CancellationToken.None);

        // Assert
        await _sonarrClient.Received(1).AddSeriesAsync(
            Arg.Is<AddSeriesRequest>(r =>
                r.TvdbId == 12345 &&
                r.Title == "Test Program" &&
                r.QualityProfileId == 7 &&
                r.RootFolderPath == "/media/tv" &&
                r.Monitored &&
                r.SeasonFolder &&
                !r.AddOptions.SearchForMissingEpisodes),
            Arg.Any<CancellationToken>());
    }

    private static Series CreateSonarrSeries(int id, int tvdbId) => new(
        Title: "Test",
        SortTitle: "test",
        Status: "continuing",
        Ended: false,
        Overview: "",
        Airtime: TimeOnly.MinValue,
        Originallanguage: new Originallanguage(1, "English"),
        Year: 2024,
        Path: "/tv/test",
        QualityProfileId: 1,
        SeasonFolder: true,
        Monitored: true,
        MonitorNewItems: "all",
        UseScheneNumbering: false,
        Runtime: 30,
        TvdbId: tvdbId,
        TvRageId: 0,
        TvMazeId: 0,
        TmdbId: 0,
        SeriesType: "standard",
        CleanTitle: "test",
        ImdbId: "",
        TitleSlug: "test",
        Certification: "",
        FirstAired: DateTime.UtcNow,
        LastAired: DateTime.UtcNow,
        Added: DateTime.UtcNow,
        Images: [],
        Seasons: [],
        Genres: [],
        Ratings: new Ratings(0, 0),
        LanguageProfileId: 1,
        Id: id);
}
