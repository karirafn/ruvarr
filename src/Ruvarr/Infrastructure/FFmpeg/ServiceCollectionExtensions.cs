namespace Ruvarr.Infrastructure.FFmpeg;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddFfmpeg(this IServiceCollection services)
    {
        services.AddScoped<IFfmpegService, FfmpegService>();

        return services;
    }
}
