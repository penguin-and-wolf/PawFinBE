using System;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>Provides the user's resumable playback items through Pawfin.</summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/ContinueWatching")]
[Produces("application/json")]
public class ContinueWatchingController : ControllerBase
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IDtoService _dtoService;

    /// <summary>Initializes a new instance of the <see cref="ContinueWatchingController"/> class.</summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="userDataManager">The Jellyfin user data manager.</param>
    /// <param name="dtoService">The Jellyfin DTO service.</param>
    public ContinueWatchingController(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, IDtoService dtoService)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _dtoService = dtoService;
    }

    /// <summary>Gets the authenticated user's resumable video items.</summary>
    /// <param name="startIndex">The first result to return.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <returns>The resumable video items.</returns>
    [HttpGet]
    public ActionResult<QueryResult<BaseItemDto>> Get([FromQuery] int startIndex = 0, [FromQuery] int limit = 100)
    {
        var userId = UserHelper.GetUserId(User);
        var user = userId == Guid.Empty ? null : _userManager.GetUserById(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var offset = Math.Max(startIndex, 0);
        var options = new DtoOptions(true);
        var query = new InternalItemsQuery(user)
        {
            Recursive = true,
            IsResumable = true,
        };
        var resumable = _libraryManager.GetItemList(query)
            .Where(item => item.MediaType == MediaType.Video)
            .Where(item => _userDataManager.GetUserData(user, item).PlaybackPositionTicks > 0 && !_userDataManager.GetUserData(user, item).Played)
            .OrderByDescending(item => _userDataManager.GetUserData(user, item).LastPlayedDate)
            .ToList();
        var page = resumable.Skip(offset).Take(Math.Clamp(limit, 0, 500))
            .Select(item => _dtoService.GetBaseItemDto(item, options, user)).ToList();
        return new QueryResult<BaseItemDto>(resumable.Count, offset, page);
    }
}
