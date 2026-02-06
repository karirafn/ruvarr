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

        return services;
    }
}