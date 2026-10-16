using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>Controls which users can see Moments collections.</summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/Moments/Access")]
[Produces("application/json")]
public class MomentAccessController : ControllerBase
{
    private readonly PawfinStore _store;
    private readonly IUserManager _userManager;

    /// <summary>Initializes a new instance of the <see cref="MomentAccessController"/> class.</summary>
    /// <param name="store">The Pawfin store.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    public MomentAccessController(PawfinStore store, IUserManager userManager)
    {
        _store = store;
        _userManager = userManager;
    }

    /// <summary>Gets the current user's Moments access and, for administrators, the global allow list.</summary>
    /// <returns>The current access setting.</returns>
    [HttpGet]
    public ActionResult<MomentAccessDto> Get()
    {
        var userId = UserHelper.GetUserId(User);
        if (userId == Guid.Empty || _userManager.GetUserById(userId) is null)
        {
            return Unauthorized();
        }

        var isAdministrator = UserHelper.IsAdministrator(User, _userManager);
        var allowedUserIds = _store.Read(data => data.MomentAllowedUsers.ToArray());
        return new MomentAccessDto
        {
            Allowed = allowedUserIds.Contains(userId),
            AllowedUserIds = isAdministrator ? allowedUserIds : null,
        };
    }

    /// <summary>Sets the global users allowed to view Moments.</summary>
    /// <param name="request">The set of allowed Jellyfin user ids.</param>
    /// <returns>The saved access list.</returns>
    [HttpPut]
    public ActionResult<MomentAccessDto> Update([FromBody] MomentAccessRequest request)
    {
        if (!UserHelper.IsAdministrator(User, _userManager))
        {
            return Forbid();
        }

        var userId = UserHelper.GetUserId(User);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var allowedUserIds = request.UserIds
            .Where(id => id != Guid.Empty && _userManager.GetUserById(id) is not null)
            .ToHashSet();
        _store.Write(data =>
        {
            data.MomentAllowedUsers = allowedUserIds;
            return true;
        });

        return new MomentAccessDto
        {
            Allowed = allowedUserIds.Contains(userId),
            AllowedUserIds = allowedUserIds,
        };
    }
}
