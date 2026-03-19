namespace Ruvarr.Settings.Queries.TestSonarrConnection;

public sealed record TestSonarrConnectionQuery(string SonarrBaseAddress, string SonarrApiKey, bool UseStoredApiKey);
