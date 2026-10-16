using System;
using System.Collections.Generic;
using MediaBrowser.Model.Dto;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>A named Moments group and its chronologically ordered clips.</summary>
public class MomentGroupDto
{
    /// <summary>Gets or sets the stable group id.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the group display name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the URL for the group's optional Miniature image.</summary>
    public Uri? MiniatureUrl { get; set; }

    /// <summary>Gets or sets the URL for the group's Primary image.</summary>
    public Uri? PrimaryUrl { get; set; }

    /// <summary>Gets or sets image URLs keyed by the Jellyfin clip item id.</summary>
    public Dictionary<string, Uri> ClipImageUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the ordered clips in the group.</summary>
    public List<BaseItemDto> Clips { get; set; } = [];
}
