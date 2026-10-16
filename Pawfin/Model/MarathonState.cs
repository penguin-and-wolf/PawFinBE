using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>Server-persisted playback position and order for one user's active marathon.</summary>
public class MarathonState
{
    /// <summary>Gets or sets the identifier of this saved marathon.</summary>
    public Guid MarathonId { get; set; }

    /// <summary>Gets or sets the ordered Jellyfin item ids.</summary>
    public List<Guid> ItemIds { get; set; } = [];

    /// <summary>Gets or sets the index of the item currently being watched.</summary>
    public int CurrentIndex { get; set; }

    /// <summary>Gets or sets the optional source asset owner type, such as Seasonals or Collections.</summary>
    public string? SourceType { get; set; }

    /// <summary>Gets or sets the optional source asset owner id.</summary>
    public Guid? SourceId { get; set; }

    /// <summary>Gets or sets the display name for the source group.</summary>
    public string? SourceName { get; set; }

    /// <summary>Gets or sets when this state was last updated.</summary>
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
