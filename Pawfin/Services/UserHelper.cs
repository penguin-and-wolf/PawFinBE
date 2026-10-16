using System;
using System.Security.Claims;
using MediaBrowser.Controller.Library;

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
        var value = principal.FindFirst("Jellyfin-UserId")?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }

    /// <summary>
    /// Checks whether the authenticated Jellyfin user is an administrator.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <returns>Whether the user exists and has administrator policy.</returns>
    public static bool IsAdministrator(ClaimsPrincipal principal, IUserManager userManager)
    {
        var userId = GetUserId(principal);
        return userId != Guid.Empty && userManager.GetUserById(userId)?.Policy.IsAdministrator == true;
    }
}
