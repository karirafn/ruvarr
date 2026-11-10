namespace Ruvarr.Programs.Domain;

/// <summary>How a RÚV episode's TVDB links were made.</summary>
internal enum MatchOrigin
{
    /// <summary>Linked by a matching strategy.</summary>
    Automatic,

    /// <summary>Linked by a user action.</summary>
    Manual,
}
