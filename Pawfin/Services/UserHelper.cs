using System;
using System.Security.Claims;

namespace PenguinAndWolf.Pawfin.Services;

/// <summary>
/// Helpers for reading the authenticated Jellyfin user.
/// </summary>
public static class UserHelper
{
    /// <summary>
    /// Gets the id of the authenticated user.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <returns>The user id, or empty.</returns>
    public static Guid GetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("Jellyfin-UserId")?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
