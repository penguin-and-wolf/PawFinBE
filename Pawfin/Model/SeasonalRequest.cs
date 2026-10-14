using System;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// Create/update payload for a seasonal. Null properties are left unchanged on update.
/// </summary>
public class SeasonalRequest
{
    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the overview.</summary>
    public string? Overview { get; set; }

    /// <summary>Gets or sets the start date.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Gets or sets the end date.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the range repeats yearly.</summary>
    public bool? Recurring { get; set; }

    /// <summary>Gets or sets the order mode.</summary>
    public SeasonalOrderMode? OrderMode { get; set; }

    /// <summary>Gets or sets the initial item ids (create only).</summary>
    public Guid[]? ItemIds { get; set; }

    /// <summary>Gets or sets a value indicating whether dates should be cleared (update only).</summary>
    public bool ClearDates { get; set; }

    /// <summary>Gets or sets a value indicating whether the optional end date should be cleared.</summary>
    public bool ClearEndDate { get; set; }
}
