using System;
using PenguinAndWolf.Pawfin.Model;
using Xunit;

namespace Pawfin.Tests;

public class SeasonalTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    [Fact]
    public void NoDates_AlwaysActive() => Assert.True(new Seasonal().IsActiveOn(D(2026, 5, 5)));

    [Fact]
    public void SingleDate_OnlyThatDay()
    {
        var s = new Seasonal { StartDate = D(2026, 10, 31) };
        Assert.True(s.IsActiveOn(D(2026, 10, 31)));
        Assert.False(s.IsActiveOn(D(2026, 11, 1)));
    }

    [Fact]
    public void Range_IsInclusive()
    {
        var s = new Seasonal { StartDate = D(2026, 12, 1), EndDate = D(2026, 12, 25) };
        Assert.True(s.IsActiveOn(D(2026, 12, 1)));
        Assert.True(s.IsActiveOn(D(2026, 12, 25)));
        Assert.False(s.IsActiveOn(D(2027, 12, 10)));
    }

    [Fact]
    public void RecurringRange_WrapsYearEnd()
    {
        var s = new Seasonal { StartDate = D(2026, 12, 20), EndDate = D(2027, 1, 5), Recurring = true };
        Assert.True(s.IsActiveOn(D(2030, 12, 31)));
        Assert.True(s.IsActiveOn(D(2030, 1, 2)));
        Assert.False(s.IsActiveOn(D(2030, 6, 1)));
    }
}
