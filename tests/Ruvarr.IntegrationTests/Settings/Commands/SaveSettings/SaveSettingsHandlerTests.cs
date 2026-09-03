using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Ruvarr.Abstractions;
using Ruvarr.Downloads.Domain;
using Ruvarr.Programs.Domain;
using Ruvarr.Settings;
using Ruvarr.Settings.Commands.SaveSettings;

using Shouldly;

namespace Ruvarr.IntegrationTests.Settings.Commands.SaveSettings;

public sealed class SaveSettingsHandlerTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        RuvarrDbContext dbContext = scope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        await factory.ResetSettingsAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task DeletesProgramsForNewlyIgnoredChannels()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "IgnoreMe", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            ["IgnoreMe"],
            []);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task CascadeDeletesDownloadQueueItemsForDeletedPrograms()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "IgnoreMe", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        program.TryAddEpisode("EP001", new Uri("https://example.com/ep.mp4"), "Episode 1", "Desc", DateTime.UtcNow, TimeSpan.FromMinutes(30));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvEpisode episode = program.Episodes[0];
        arrangeContext.Set<DownloadQueueItem>().Add(DownloadQueueItem.Create(episode));
        await arrangeContext.SaveChangesAsync(cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            ["IgnoreMe"],
            []);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int programCount = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        int downloadCount = await verifyContext.Set<DownloadQueueItem>().CountAsync(cancellationToken);
        programCount.ShouldBe(0);
        downloadCount.ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotDeleteProgramsForPreviouslyIgnoredChannels()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        ISettingsStore store = arrangeScope.ServiceProvider.GetRequiredService<ISettingsStore>();

        RuvProgram program = RuvProgram.Create(1, "AlreadyIgnored", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvarrSettings preExisting = store.Current with { IgnoredChannels = ["AlreadyIgnored"] };
        await store.SaveAsync(preExisting, cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            ["AlreadyIgnored"],
            []);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task HandlesEmptyNewlyIgnoredSetGracefully()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();

        RuvProgram program = RuvProgram.Create(1, "RUV1", "Test Program", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            [],
            []);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task DeletesProgramsForNewlyIgnoredProgramTitles()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        ISettingsStore store = arrangeScope.ServiceProvider.GetRequiredService<ISettingsStore>();

        RuvarrSettings preExisting = store.Current with { IgnoredPrograms = [] };
        await store.SaveAsync(preExisting, cancellationToken);

        RuvProgram program = RuvProgram.Create(1, "RUV", "Test Show", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            [],
            ["Test Show"]);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task DeletesProgramsForNewlyIgnoredProgramTitlesCaseInsensitive()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        ISettingsStore store = arrangeScope.ServiceProvider.GetRequiredService<ISettingsStore>();

        RuvarrSettings preExisting = store.Current with { IgnoredPrograms = [] };
        await store.SaveAsync(preExisting, cancellationToken);

        RuvProgram program = RuvProgram.Create(1, "RUV", "test show", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            [],
            ["Test Show"]);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(0);
    }

    [Fact]
    public async Task DoesNotDeleteProgramsForPreviouslyIgnoredProgramTitles()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope arrangeScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext arrangeContext = arrangeScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        ISettingsStore store = arrangeScope.ServiceProvider.GetRequiredService<ISettingsStore>();

        RuvProgram program = RuvProgram.Create(1, "RUV", "Test Show", null, multipleEpisodes: true);
        arrangeContext.Set<RuvProgram>().Add(program);
        await arrangeContext.SaveChangesAsync(cancellationToken);

        RuvarrSettings preExisting = store.Current with { IgnoredPrograms = ["Test Show"] };
        await store.SaveAsync(preExisting, cancellationToken);

        SaveSettingsCommand command = new(
            new Uri("http://localhost:8989"),
            "apikey",
            "tvdbkey",
            "tmdbkey",
            "tv",
            "movies",
            [],
            ["Test Show"]);

        // Act
        await using AsyncServiceScope actScope = factory.Services.CreateAsyncScope();
        IRequestHandler<SaveSettingsCommand> handler =
            actScope.ServiceProvider.GetRequiredService<IRequestHandler<SaveSettingsCommand>>();
        RuvarrResult result = await handler.Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();

        await using AsyncServiceScope verifyScope = factory.Services.CreateAsyncScope();
        RuvarrDbContext verifyContext = verifyScope.ServiceProvider.GetRequiredService<RuvarrDbContext>();
        int count = await verifyContext.Set<RuvProgram>().CountAsync(cancellationToken);
        count.ShouldBe(1);
    }
}
