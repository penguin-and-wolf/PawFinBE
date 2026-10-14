namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// How the items of a seasonal are ordered.
/// </summary>
public enum SeasonalOrderMode
{
    /// <summary>Order set explicitly by the user.</summary>
    Specific,

    /// <summary>Shuffled on every request.</summary>
    Random,

    /// <summary>Oldest premiere date first.</summary>
    Chronological,

    /// <summary>Newest premiere date first.</summary>
    ReverseChronological,

    /// <summary>By sort name.</summary>
    Alphabetical,

    /// <summary>Most recently added to the library first.</summary>
    DateAdded
}
