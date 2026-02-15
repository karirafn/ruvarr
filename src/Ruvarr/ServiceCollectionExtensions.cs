using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.FFmpeg;
using Ruvarr.Ruv;
using Ruvarr.Ruv.Jobs;
using Ruvarr.Sonarr;
using Ruvarr.Sonarr.jobs;
using Ruvarr.Tmdb.Jobs;
using Ruvarr.Tvdb;
using Ruvarr.Tvdb.Jobs;

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
        services.AddSonarr();
        services.AddRuv();

        services.AddQuartz(options =>
        {
            JobKey ruvSeriesSync = new(nameof(RuvProgramSyncJob));
            options.AddJob<RuvProgramSyncJob>(x => x.WithIdentity(ruvSeriesSync))
                .AddTrigger(trigger => trigger
                    .ForJob(ruvSeriesSync)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInHours(6)
                    .RepeatForever()));

            // Start this job 30 seconds after series sync job to ensure series exist on first run
            JobKey ruvEpisodeSync = new(nameof(RuvEpisodesSyncJob));
            options.AddJob<RuvEpisodesSyncJob>(x => x.WithIdentity(ruvEpisodeSync))
                .AddTrigger(trigger => trigger
                    .ForJob(ruvEpisodeSync)
                    .StartAt(DateTimeOffset.UtcNow.AddSeconds(30))
                    .WithSimpleSchedule(x => x.WithIntervalInHours(6)
                    .RepeatForever()));

            JobKey tmdbMovieLookup = new(nameof(TmdbMovieLookupJob));
            options.AddJob<TmdbMovieLookupJob>(x => x.WithIdentity(tmdbMovieLookup))
                .AddTrigger(trigger => trigger
                    .ForJob(tmdbMovieLookup)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

            JobKey tvdbSeriesLookup = new(nameof(TvdbSeriesLookupJob));
            options.AddJob<TvdbSeriesLookupJob>(x => x.WithIdentity(tvdbSeriesLookup))
                .AddTrigger(trigger => trigger
                    .ForJob(tvdbSeriesLookup)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

            JobKey tvdbEpisodeLookup = new(nameof(TvdbEpisodeLookupJob));
            options.AddJob<TvdbEpisodeLookupJob>(x => x.WithIdentity(tvdbEpisodeLookup))
                .AddTrigger(trigger => trigger
                    .ForJob(tvdbEpisodeLookup)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

            JobKey downloadMissing = new(nameof(DownloadMissingEpisodesJob));
            options.AddJob<DownloadMissingEpisodesJob>(x => x.WithIdentity(downloadMissing))
                .AddTrigger(trigger => trigger
                    .ForJob(downloadMissing)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInHours(1)
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