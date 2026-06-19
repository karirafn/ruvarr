using Microsoft.EntityFrameworkCore;

using Quartz;

using Ruvarr.Abstractions;
using Ruvarr.Dashboard;
using Ruvarr.Downloads;
using Ruvarr.Infrastructure.FFmpeg;
using Ruvarr.Programs;
using Ruvarr.Infrastructure.Ruv;
using Ruvarr.Infrastructure.Sonarr;
using Ruvarr.Infrastructure.Tmdb;
using Ruvarr.Infrastructure.Tvdb;
using Ruvarr.Jobs;
using Ruvarr.ProgramRefreshQueue.Notifiers;
using Ruvarr.Settings;
using Ruvarr.TvdbEpisodeLookup;
using Ruvarr.TvdbEpisodeLookup.Jobs;
using Ruvarr.TvdbEpisodeLookup.Notifiers;
using Ruvarr.TvdbEpisodeLookup.Strategies;
using Ruvarr.TvdbSeriesLookup.Jobs;
using Ruvarr.TvdbSeriesLookup.Notifiers;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services, string dbConnectionString, string settingsFilePath)
    {
        services.AddSettings(settingsFilePath);
        services.AddSingleton<IDomainEventBroadcaster, DomainEventBroadcaster>();
        services.AddSingleton<ProgramRefreshNotifier>();
        services.AddSingleton<TvdbSeriesLookupNotifier>();
        services.AddSingleton<TvdbEpisodeLookupNotifier>();
        services.AddMemoryCache(options => options.SizeLimit = 512);
        services.AddFfmpeg();
        services.AddTmdb();
        services.AddTvdb();
        services.AddSonarr();
        services.AddRuv();
        // Episode matching strategies — ORDER MATTERS: translation first, then episode number, then part-one sibling
        services.AddScoped<IEpisodeMatchingStrategy, TranslationMatchingStrategy>();
        services.AddScoped<IEpisodeMatchingStrategy, EpisodeNumberMatchingStrategy>();
        services.AddScoped<IEpisodeMatchingStrategy, PartOneSiblingMatchingStrategy>();
        services.AddScoped<ITvdbEpisodeMatcher, TvdbEpisodeMatcher>();

        services.AddDashboard();
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

            JobKey tvdbEpisodeLookupRetry = new(nameof(TvdbEpisodeLookupRetryJob));
            options.AddJob<TvdbEpisodeLookupRetryJob>(x => x.WithIdentity(tvdbEpisodeLookupRetry))
                .AddTrigger(trigger => trigger
                    .ForJob(tvdbEpisodeLookupRetry)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInMinutes(10)
                    .RepeatForever()));

            JobKey downloadQueue = new(nameof(DownloadQueueProcessor));
            options.AddJob<DownloadQueueProcessor>(x => x.WithIdentity(downloadQueue))
                .AddTrigger(trigger => trigger
                    .ForJob(downloadQueue)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInSeconds(5)
                    .RepeatForever()));

            JobKey tvdbIslTranslationBackfill = new(nameof(TvdbIslTranslationBackfillJob));
            options.AddJob<TvdbIslTranslationBackfillJob>(x => x.WithIdentity(tvdbIslTranslationBackfill))
                .AddTrigger(trigger => trigger
                    .ForJob(tvdbIslTranslationBackfill)
                    .StartNow());
        });
        services.AddQuartzHostedService(x => x.WaitForJobsToComplete = true);

        services.AddDbContext<RuvarrDbContext>(options =>
        {
            options.UseSqlite(dbConnectionString);
            options.UseSnakeCaseNamingConvention();
        });

        return services;
    }

    private static void AddTmdb(this IServiceCollection services)
    {
        services.AddSingleton<TmdbClientProvider>();
    }
}