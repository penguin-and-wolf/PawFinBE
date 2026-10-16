using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>Updates the global list of users allowed to view moments.</summary>
public class MomentAccessRequest
{
    /// <summary>Gets or sets the allowed Jellyfin user ids.</summary>
    public List<Guid> UserIds { get; set; } = [];
}
