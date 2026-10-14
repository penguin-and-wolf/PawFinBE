using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// A collection-like grouping that is active during a date range.
/// </summary>
public class Seasonal
{
    /// <summary>Gets or sets the id.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the overview.</summary>
    public string? Overview { get; set; }

    /// <summary>Gets or sets the required first active month and day.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Gets or sets the last active month and day. Null means only the start day.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Gets or sets a value indicating whether the range repeats every year (year is ignored).</summary>
    public bool Recurring { get; set; } = true;

    /// <summary>Gets or sets the order mode.</summary>
    public SeasonalOrderMode OrderMode { get; set; } = SeasonalOrderMode.Specific;

    /// <summary>Gets or sets the items.</summary>
    public List<SeasonalItem> Items { get; set; } = [];

    /// <summary>Gets or sets the images, backdrops and themes.</summary>
    public AssetSet Assets { get; set; } = new();

    /// <summary>Gets or sets the creation time.</summary>
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Returns whether the seasonal is active on a day.
    /// </summary>
    /// <param name="day">The day to test.</param>
    /// <returns>True when active.</returns>
    public bool IsActiveOn(DateTime day)
    {
        if (StartDate is null)
        {
            return false;
        }

        var start = StartDate.Value.Date;
        var end = (EndDate ?? StartDate.Value).Date;
        var d = day.Date;

        var k = Key(d);
        var ks = Key(start);
        var ke = Key(end);
        return ks <= ke ? k >= ks && k <= ke : k >= ks || k <= ke;
    }

    private static int Key(DateTime x) => (x.Month * 100) + x.Day;
}
