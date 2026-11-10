using Ruvarr.Abstractions;
using Ruvarr.Infrastructure.Sonarr.Models;

namespace Ruvarr.Programs.Components;

internal abstract record SonarrConfigLoadState
{
    private SonarrConfigLoadState()
    {
    }

    public static SonarrConfigLoadState From(
        Result<IReadOnlyList<RootFolder>> rootFolders,
        Result<IReadOnlyList<QualityProfile>> qualityProfiles)
    {
        ArgumentNullException.ThrowIfNull(rootFolders);
        ArgumentNullException.ThrowIfNull(qualityProfiles);

        if (rootFolders.IsFailure || qualityProfiles.IsFailure)
        {
            return new Unreachable();
        }

        return new Loaded(rootFolders.FromResult(), qualityProfiles.FromResult());
    }

    public sealed record Loading : SonarrConfigLoadState;

    public sealed record Unreachable : SonarrConfigLoadState;

    public sealed record Loaded(
        IReadOnlyList<RootFolder> RootFolders,
        IReadOnlyList<QualityProfile> QualityProfiles) : SonarrConfigLoadState
    {
        public bool HasNoRootFolders => RootFolders.Count == 0;

        public bool HasNoQualityProfiles => QualityProfiles.Count == 0;

        public bool IsIncomplete => HasNoRootFolders || HasNoQualityProfiles;
    }
}
