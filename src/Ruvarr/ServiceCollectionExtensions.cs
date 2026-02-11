using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.FFmpeg;
using Ruvarr.Jobs;
using Ruvarr.Ruv;
using Ruvarr.Tvdb;

using TMDbLib.Client;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services, string dbConnectionString)
    {
        services.AddMemoryCache();
        services.AddFfmpeg();
        services.AddTmdb();
        services.AddTvdb();
        services.AddRuv();

        services.AddQuartz(options =>
        {
            JobKey seriesSync = new(nameof(RuvProgramSyncJob));
            options.AddJob<RuvProgramSyncJob>(x => x.WithIdentity(seriesSync))
                .AddTrigger(trigger => trigger
                    .ForJob(seriesSync)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInHours(6)
                    .RepeatForever()));

            JobKey tmdbLookup = new(nameof(TmdbLookupJob));
            options.AddJob<TmdbLookupJob>(x => x.WithIdentity(tmdbLookup))
                .AddTrigger(trigger => trigger
                    .ForJob(tmdbLookup)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

            JobKey tvdbLookup = new(nameof(TvdbLookupJob));
            options.AddJob<TvdbLookupJob>(x => x.WithIdentity(tvdbLookup))
                .AddTrigger(trigger => trigger
                    .ForJob(tvdbLookup)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

        });
        services.AddQuartzHostedService(x => x.WaitForJobsToComplete = true);

        services.AddDbContext<RuvarrDbContext>(options =>
        {
            options.UseSqlite(dbConnectionString);
            options.UseSnakeCaseNamingConvention();
        });

        using RuvarrDbContext dbContext = services.BuildServiceProvider()
            .GetRequiredService<RuvarrDbContext>();

        dbContext.Database.EnsureCreated();
        dbContext.Database.Migrate();

        return services;
    }

    private static IServiceCollection AddTmdb(this IServiceCollection services)
    {
        services.AddOptions<TmdbOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection("Tmdb").Bind(options));

        IOptions<TmdbOptions> tmdb = services.BuildServiceProvider()
            .GetRequiredService<IOptions<TmdbOptions>>();

        services.AddScoped(x => new TMDbClient(tmdb.Value.ApiKey));

        return services;
    }

    private sealed class TmdbOptions
    {
#pragma warning disable S3459 // Unassigned members should be removed
#pragma warning disable S1144 // Unused private types or members should be removed
        public required string ApiKey { get; init; }
#pragma warning restore S1144 // Unused private types or members should be removed
#pragma warning restore S3459 // Unassigned members should be removed
    };
}