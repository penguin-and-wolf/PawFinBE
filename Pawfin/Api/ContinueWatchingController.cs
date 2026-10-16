using System;
using System.Linq;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
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
    private readonly IDtoService _dtoService;

    /// <summary>Initializes a new instance of the <see cref="ContinueWatchingController"/> class.</summary>
    public ContinueWatchingController(ILibraryManager libraryManager, IUserManager userManager, IDtoService dtoService)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _dtoService = dtoService;
    }

    /// <summary>Gets the authenticated user's resumable video items.</summary>
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
            MediaTypes = [MediaType.Video],
        };
        var resumable = _libraryManager.GetItemList(query)
            .Where(item => item.GetUserData(user).PlaybackPositionTicks > 0 && !item.GetUserData(user).Played)
            .OrderByDescending(item => item.GetUserData(user).LastPlayedDate)
            .ToList();
        var page = resumable.Skip(offset).Take(Math.Clamp(limit, 0, 500))
            .Select(item => _dtoService.GetBaseItemDto(item, options, user)).ToList();
        return new QueryResult<BaseItemDto>(resumable.Count, offset, page);
    }
}
