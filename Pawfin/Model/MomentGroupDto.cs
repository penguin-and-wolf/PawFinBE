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
    public string? MiniatureUrl { get; set; }

    /// <summary>Gets or sets the ordered clips in the group.</summary>
    public List<BaseItemDto> Clips { get; set; } = [];
}
