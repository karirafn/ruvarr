using Microsoft.Extensions.DependencyInjection;

using Ruvarr.FFmpeg;
using Ruvarr.Ruv;
using Ruvarr.Tvdb;

namespace Ruvarr;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRuvarr(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddFfmpeg();
        services.AddTvdb();
        services.AddRuv();

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