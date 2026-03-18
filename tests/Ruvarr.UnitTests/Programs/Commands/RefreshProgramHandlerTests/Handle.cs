using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Ruvarr.Abstractions;
using Ruvarr.Programs;
using Ruvarr.Programs.Commands.RefreshProgram;
using Ruvarr.Programs.Domain;
using Ruvarr.Testing.Builders;

using Shouldly;

namespace Ruvarr.UnitTests.Programs.Commands.RefreshProgramHandlerTests;

public sealed class Handle
{
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

    private static RefreshProgramHandler CreateHandler(RuvarrDbContext dbContext) => new(dbContext);

    [Fact]
    public async Task ReturnsProgramNotFoundWhenProgramDoesNotExist()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new RefreshProgramCommand(999), CancellationToken.None);

        // Assert
        result.Error.ShouldBe(ProgramErrors.ProgramNotFound);
    }

    [Fact]
    public async Task ReturnsSuccessWhenProgramExists()
    {
        // Arrange
        using RuvarrDbContext dbContext = CreateDbContext();
        RuvProgram program = new RuvProgramBuilder().WithRuvId(1).Build();
        dbContext.Set<RuvProgram>().Add(program);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        RefreshProgramHandler sut = CreateHandler(dbContext);

        // Act
        RuvarrResult result = await sut.Handle(new RefreshProgramCommand(1), CancellationToken.None);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}
