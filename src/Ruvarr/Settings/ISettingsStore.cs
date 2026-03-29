namespace Ruvarr.Settings;

internal interface ISettingsStore
{
    RuvarrSettings Current { get; }

    event Action? SettingsChanged;

    Task SaveAsync(RuvarrSettings settings, CancellationToken cancellationToken);
}
