using System;
using System.Collections.Generic;

namespace PenguinAndWolf.Pawfin.Model;

/// <summary>Moment-viewing permission for the current user and (for admins) the global allow list.</summary>
public class MomentAccessDto
{
    /// <summary>Gets or sets whether the current user can view moments.</summary>
    public bool Allowed { get; set; }

    /// <summary>Gets or sets the global allow list, included only for administrators.</summary>
    public IReadOnlyCollection<Guid>? AllowedUserIds { get; set; }
}

/// <summary>Updates the global list of users allowed to view moments.</summary>
public class MomentAccessRequest
{
    /// <summary>Gets or sets the allowed Jellyfin user ids.</summary>
    public List<Guid> UserIds { get; set; } = [];
}
