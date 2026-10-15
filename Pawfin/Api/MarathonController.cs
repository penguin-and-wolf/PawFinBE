using System;
using System.Linq;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>Stores a user's active marathon so Continue Watching can resume the ordered queue on any device.</summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/Marathon")]
[Produces("application/json")]
public class MarathonController : ControllerBase
{
    private readonly PawfinStore _store;

    /// <summary>Initializes a new instance of the <see cref="MarathonController"/> class.</summary>
    /// <param name="store">The persisted Pawfin store.</param>
    public MarathonController(PawfinStore store) => _store = store;

    /// <summary>Gets the user's active marathon, if one exists.</summary>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>The active queue or not found.</returns>
    [HttpGet]
    public ActionResult<MarathonState> Get([FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty) return Unauthorized();
        var state = _store.Read(data => data.ActiveMarathons.TryGetValue(userId, out var value) ? value : null);
        if (state is null) return NotFound();
        return state;
    }

    /// <summary>Saves the current ordered queue position for the user's active marathon.</summary>
    /// <param name="state">The ordered queue and current item index.</param>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>The saved queue state.</returns>
    [HttpPut]
    public ActionResult<MarathonState> Save(
        [FromBody] MarathonState state,
        [FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty) return Unauthorized();
        if (state.ItemIds is null || state.ItemIds.Count == 0 || state.ItemIds.Count > 500 ||
            state.ItemIds.Any(id => id == Guid.Empty) || state.ItemIds.Distinct().Count() != state.ItemIds.Count ||
            state.CurrentIndex < 0 || state.CurrentIndex >= state.ItemIds.Count ||
            (state.SourceType is not null && !string.Equals(state.SourceType, "Seasonals", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(state.SourceType, "Collections", StringComparison.OrdinalIgnoreCase)) ||
            (state.SourceType is not null && state.SourceId is null))
        {
            return BadRequest("A marathon must contain 1–500 unique item ids and a valid current index.");
        }

        state.UpdatedUtc = DateTime.UtcNow;
        _store.Write(data => data.ActiveMarathons[userId] = state);
        return Ok(state);
    }

    /// <summary>Clears the user's active marathon after it is completed or dismissed.</summary>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>No content.</returns>
    [HttpDelete]
    public ActionResult Clear([FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty) return Unauthorized();
        _store.Write(data => data.ActiveMarathons.Remove(userId));
        return NoContent();
    }

    private Guid ResolveUserId(Guid? requestedUserId)
    {
        var authenticatedUserId = UserHelper.GetUserId(User);
        return authenticatedUserId != Guid.Empty ? authenticatedUserId : requestedUserId.GetValueOrDefault();
    }
}
