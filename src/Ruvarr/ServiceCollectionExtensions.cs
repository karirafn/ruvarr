using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Quartz;

using Ruvarr.Downloads;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Jobs;
using Ruvarr.Programs;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tvdb;

using TMDbLib.Client;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services, string dbConnectionString)
    {
        services.AddOptions<RuvarrOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(RuvarrOptions.SectionName).Bind(options));

        services.AddSingleton<ProgramRefreshNotifier>();
        services.AddMemoryCache();
        services.AddFfmpeg();
        services.AddTmdb();
        services.AddTvdb();
        services.AddSonarr();
        services.AddRuv();
        services.AddPrograms();
        services.AddDownloads();

        services.AddQuartz(options =>
        {
            JobKey ruvSeriesSync = new(nameof(RuvProgramRefreshJob));
            options.AddJob<RuvProgramRefreshJob>(x => x.WithIdentity(ruvSeriesSync))
                .AddTrigger(trigger => trigger
                    .ForJob(ruvSeriesSync)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInHours(1)
                    .RepeatForever()));

            JobKey ruvEpisodeSync = new(nameof(RuvEpisodesSyncJob));
            options.AddJob<RuvEpisodesSyncJob>(x => x.WithIdentity(ruvEpisodeSync))
                .AddTrigger(trigger => trigger
                    .ForJob(ruvEpisodeSync)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
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
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(1)
                    .RepeatForever()));

            JobKey downloadQueue = new(nameof(DownloadQueueProcessor));
            options.AddJob<DownloadQueueProcessor>(x => x.WithIdentity(downloadQueue))
                .AddTrigger(trigger => trigger
                    .ForJob(downloadQueue)
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