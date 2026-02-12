using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ruvarr.FFmpeg;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddFfmpeg(this IServiceCollection services)
    {
        services.AddOptions<FfmpegOptions>()
            .Configure<IConfiguration>((options, configuration)
                => configuration.GetRequiredSection(FfmpegOptions.SectionName).Bind(options));

        services.AddScoped<IFfmpegService, FfmpegService>();

        return services;
    }
}