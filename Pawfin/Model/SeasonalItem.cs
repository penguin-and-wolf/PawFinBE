using System;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// An entry in a seasonal.
/// </summary>
public class SeasonalItem
{
    /// <summary>Gets or sets the library item id (movie, series, episode, collection...).</summary>
    public Guid ItemId { get; set; }

    /// <summary>Gets or sets the position used by <see cref="SeasonalOrderMode.Specific"/>.</summary>
    public int Order { get; set; }
}
