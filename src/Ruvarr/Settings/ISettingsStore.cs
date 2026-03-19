namespace Ruvarr.Settings;

internal interface ISettingsStore
{
    RuvarrSettings Current { get; }

    Task SaveAsync(RuvarrSettings settings, CancellationToken cancellationToken);
}
