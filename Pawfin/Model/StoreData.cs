using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// Everything persisted by the plugin.
/// </summary>
public class StoreData
{
    /// <summary>Gets or sets the seasonals.</summary>
    public List<Seasonal> Seasonals { get; set; } = [];

    /// <summary>Gets or sets assets for existing Jellyfin collections, by collection id.</summary>
    public Dictionary<Guid, AssetSet> CollectionAssets { get; set; } = [];

    /// <summary>Gets or sets the hidden Next Up entries per user (series or episode ids).</summary>
    public Dictionary<Guid, HashSet<Guid>> HiddenNextUp { get; set; } = [];

    /// <summary>Gets or sets the legacy single active marathon for older app versions.</summary>
    public Dictionary<Guid, MarathonState> ActiveMarathons { get; set; } = [];

    /// <summary>Gets or sets all resumable marathons for each user, keyed by marathon id.</summary>
    public Dictionary<Guid, Dictionary<Guid, MarathonState>> Marathons { get; set; } = [];
}
