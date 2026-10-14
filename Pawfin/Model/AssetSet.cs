using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// Images, backdrops and themes attached to a seasonal or collection.
/// File names refer to files stored in the plugin data folder.
/// </summary>
public class AssetSet
{
    /// <summary>Gets or sets single images by type (Primary, Logo, Banner, Thumb, Art, Disc, Box...).</summary>
    public Dictionary<string, AssetFile> Images { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the background images.</summary>
    public List<AssetFile> Backdrops { get; set; } = [];

    /// <summary>Gets or sets the theme songs.</summary>
    public List<AssetFile> ThemeSongs { get; set; } = [];

    /// <summary>Gets or sets the theme videos.</summary>
    public List<AssetFile> ThemeVideos { get; set; } = [];

    /// <summary>Gets or sets the trailer videos.</summary>
    public List<AssetFile> Trailers { get; set; } = [];
}
