using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Quartz;

using Ruvarr.FFmpeg;
using Ruvarr.Jobs;
using Ruvarr.Ruv;
using Ruvarr.Tvdb;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services, string connectionString)
    {
        services.AddMemoryCache();
        services.AddFfmpeg();
        services.AddTvdb();
        services.AddRuv();

        services.AddQuartz(options =>
        {
            JobKey seriesSync = new(nameof(RuvSeriesSyncJob));
            options.AddJob<RuvSeriesSyncJob>(x => x.WithIdentity(seriesSync))
                .AddTrigger(trigger => trigger
                    .ForJob(seriesSync)
                    .StartNow()
                    .WithSimpleSchedule(x => x.WithIntervalInHours(6)
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
            options.UseSqlite(connectionString);
            options.UseSnakeCaseNamingConvention();
        });

        using RuvarrDbContext dbContext = services.BuildServiceProvider()
            .GetRequiredService<RuvarrDbContext>();

        dbContext.Database.EnsureCreated();
        dbContext.Database.Migrate();

        return services;
    }
}