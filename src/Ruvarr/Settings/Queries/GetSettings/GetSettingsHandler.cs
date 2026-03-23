using Ruvarr.Abstractions;

namespace Ruvarr.Settings.Queries.GetSettings;

internal sealed class GetSettingsHandler(ISettingsStore store) : IRequestHandler<GetSettingsQuery, Result<RuvarrSettings>>
{
    public Task<Result<RuvarrSettings>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        RuvarrSettings settings = store.Current;

        if (!string.IsNullOrEmpty(settings.SonarrApiKey))
        {
            settings = settings with { SonarrApiKey = "****" };
        }

        if (!string.IsNullOrEmpty(settings.TvdbApiKey))
        {
            settings = settings with { TvdbApiKey = "****" };
        }

        if (!string.IsNullOrEmpty(settings.TmdbApiKey))
        {
            settings = settings with { TmdbApiKey = "****" };
        }

        return Task.FromResult<Result<RuvarrSettings>>(settings);
    }
}
