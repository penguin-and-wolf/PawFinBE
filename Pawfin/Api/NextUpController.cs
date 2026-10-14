using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.TV;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>
/// Lets users remove entries from their Next Up list. Stored on the server, so it applies on every device.
/// </summary>
[ApiController]
[Authorize]
[Route("Pawfin/NextUp")]
[Produces("application/json")]
public class NextUpController : ControllerBase
{
    private readonly PawfinStore _store;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IDtoService _dtoService;
    private readonly ITVSeriesManager _tvSeriesManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="NextUpController"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dtoService">The dto service.</param>
    /// <param name="tvSeriesManager">The series manager.</param>
    public NextUpController(
        PawfinStore store,
        ILibraryManager libraryManager,
        IUserManager userManager,
        IDtoService dtoService,
        ITVSeriesManager tvSeriesManager)
    {
        _store = store;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _dtoService = dtoService;
        _tvSeriesManager = tvSeriesManager;
    }

    /// <summary>
    /// Gets the user's Next Up list without the hidden entries.
    /// </summary>
    /// <param name="limit">Maximum number of items.</param>
    /// <returns>The Next Up episodes.</returns>
    [HttpGet]
    public ActionResult<QueryResult<BaseItemDto>> GetNextUp([FromQuery] int limit = 24)
    {
        var userId = UserHelper.GetUserId(User);
        var user = _userManager.GetUserById(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        var hidden = GetHidden(userId);
        var options = new DtoOptions(true);
        var next = _tvSeriesManager.GetNextUp(new NextUpQuery { User = user }, options);
        var kept = next.Items
            .Where(i => !hidden.Contains(i.Id) && !(i is Episode e && e.SeriesId != Guid.Empty && hidden.Contains(e.SeriesId)))
            .Take(Math.Max(limit, 0))
            .ToList();

        return new QueryResult<BaseItemDto>(_dtoService.GetBaseItemDtos(kept, options, user));
    }

    /// <summary>
    /// Gets the ids hidden from Next Up.
    /// </summary>
    /// <returns>The hidden ids.</returns>
    [HttpGet("Hidden")]
    public ActionResult<IReadOnlyList<Guid>> GetHiddenIds() => GetHidden(UserHelper.GetUserId(User)).ToList();

    /// <summary>
    /// Removes a series (or a single episode) from Next Up.
    /// </summary>
    /// <param name="itemId">A series id to hide the whole show, or an episode id.</param>
    /// <returns>No content.</returns>
    [HttpPost("Hidden/{itemId}")]
    public ActionResult Hide([FromRoute] Guid itemId)
    {
        var userId = UserHelper.GetUserId(User);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (_libraryManager.GetItemById(itemId) is null)
        {
            return NotFound();
        }

        _store.Write(d =>
        {
            if (!d.HiddenNextUp.TryGetValue(userId, out var set))
            {
                set = [];
                d.HiddenNextUp[userId] = set;
            }

            set.Add(itemId);
            return true;
        });
        return NoContent();
    }

    /// <summary>
    /// Puts an item back in Next Up.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Hidden/{itemId}")]
    public ActionResult Unhide([FromRoute] Guid itemId)
    {
        var userId = UserHelper.GetUserId(User);
        _store.Write(d => d.HiddenNextUp.TryGetValue(userId, out var set) && set.Remove(itemId));
        return NoContent();
    }

    private HashSet<Guid> GetHidden(Guid userId)
    {
        return _store.Read(d => d.HiddenNextUp.TryGetValue(userId, out var set) ? new HashSet<Guid>(set) : []);
    }
}
