using Ruvarr.Abstractions;

namespace Ruvarr.Infrastructure.Sonarr;

public static class SonarrErrors
{
    public const string AddSeriesFailedCode = "Sonarr.AddSeriesFailed";
    public const string InvalidRootFolderPathCode = "Sonarr.InvalidRootFolderPath";
    public const string InvalidQualityProfileIdCode = "Sonarr.InvalidQualityProfileId";

    public static readonly RuvarrError AddSeriesFailed = new(AddSeriesFailedCode, "Failed to add series to Sonarr.");
    public static readonly RuvarrError InvalidRootFolderPath = new(InvalidRootFolderPathCode, "The specified root folder path is not configured in Sonarr.");
    public static readonly RuvarrError InvalidQualityProfileId = new(InvalidQualityProfileIdCode, "The specified quality profile ID is not configured in Sonarr.");
}
