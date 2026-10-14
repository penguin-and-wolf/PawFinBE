using System;
using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// A seasonal as returned by the API.
/// </summary>
public class SeasonalDto
{
    /// <summary>Gets or sets the id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the overview.</summary>
    public string? Overview { get; set; }

    /// <summary>Gets or sets the start date.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Gets or sets the end date.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the range repeats yearly.</summary>
    public bool Recurring { get; set; }

    /// <summary>Gets or sets a value indicating whether the seasonal is active today.</summary>
    public bool IsActive { get; set; }

    /// <summary>Gets or sets the order mode.</summary>
    public SeasonalOrderMode OrderMode { get; set; }

    /// <summary>Gets or sets the number of items.</summary>
    public int ItemCount { get; set; }

    /// <summary>Gets or sets the assets.</summary>
    public AssetSet Assets { get; set; } = new();

    /// <summary>Gets or sets the ordered, visible items. Null when not requested.</summary>
    public IReadOnlyList<BaseItemDto>? Items { get; set; }
}
