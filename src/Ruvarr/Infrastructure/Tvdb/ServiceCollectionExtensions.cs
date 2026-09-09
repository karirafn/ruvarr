namespace Ruvarr.Infrastructure.Tvdb;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddTvdb(this IServiceCollection services)
    {
        services.AddTransient<TvdbAuthenticationHandler>();

        services.AddHttpClient<ITvdbClient, TvdbClient>((sp, client) =>
        {
            string baseAddress = sp.GetRequiredService<IConfiguration>()
                .GetRequiredSection("Tvdb")["BaseAddress"]
                ?? throw new InvalidOperationException("Tvdb:BaseAddress is not configured.");

            Uri uri = new(baseAddress);

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException("Tvdb:BaseAddress must use HTTPS.");
            }

            client.BaseAddress = uri;
        })
        .AddHttpMessageHandler<TvdbAuthenticationHandler>();

        return services;
    }
}
