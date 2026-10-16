using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>Stores multiple resumable marathons for each Jellyfin user.</summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/Marathons")]
[Produces("application/json")]
public class MarathonsController : ControllerBase
{
    private readonly PawfinStore _store;

    /// <summary>Initializes a new instance of the <see cref="MarathonsController"/> class.</summary>
    /// <param name="store">The persisted Pawfin store.</param>
    public MarathonsController(PawfinStore store) => _store = store;

    /// <summary>Gets all resumable marathons for the authenticated user.</summary>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>All saved marathon queues.</returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<MarathonState>> GetAll([FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        var states = _store.Write(data =>
        {
            MigrateLegacyState(data, userId);
            return data.Marathons.TryGetValue(userId, out var entries)
                ? entries.Values.OrderByDescending(state => state.UpdatedUtc)
                    .Aggregate(new List<MarathonState>(), (unique, state) =>
                    {
                        if (!unique.Any(existing => IsSameQueue(existing, state)))
                        {
                            unique.Add(state);
                        }

                        return unique;
                    }).ToArray()
                : Array.Empty<MarathonState>();
        });
        return Ok(states);
    }

    /// <summary>Creates or updates one marathon without replacing the user's other marathons.</summary>
    /// <param name="state">The ordered queue and current item index.</param>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>The saved queue state.</returns>
    [HttpPut]
    public ActionResult<MarathonState> Save(
        [FromBody] MarathonState state,
        [FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (state.ItemIds is null || state.ItemIds.Count == 0 || state.ItemIds.Count > 500 ||
            state.ItemIds.Any(id => id == Guid.Empty) || state.ItemIds.Distinct().Count() != state.ItemIds.Count ||
            state.CurrentIndex < 0 || state.CurrentIndex >= state.ItemIds.Count ||
            (state.SourceType is not null && !string.Equals(state.SourceType, "Seasonals", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(state.SourceType, "Collections", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(state.SourceType, "Moments", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(state.SourceType, "ShipMoments", StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(state.SourceType, "CharacterMoments", StringComparison.OrdinalIgnoreCase)) ||
            (state.SourceType is not null && state.SourceId is null))
        {
            return BadRequest("A marathon must contain 1–500 unique item ids and a valid current index.");
        }

        state.UpdatedUtc = DateTime.UtcNow;
        _store.Write(data =>
        {
            MigrateLegacyState(data, userId);
            if (!data.Marathons.TryGetValue(userId, out var entries))
            {
                entries = [];
                data.Marathons[userId] = entries;
            }

            if (state.MarathonId == Guid.Empty)
            {
                state.MarathonId = entries.Values
                    .Where(existing => IsSameQueue(existing, state))
                    .OrderByDescending(existing => existing.UpdatedUtc)
                    .Select(existing => existing.MarathonId)
                    .FirstOrDefault();
                if (state.MarathonId == Guid.Empty)
                {
                    state.MarathonId = Guid.NewGuid();
                }
            }

            entries[state.MarathonId] = state;
            return true;
        });
        return Ok(state);
    }

    /// <summary>Removes one completed or dismissed marathon.</summary>
    /// <param name="marathonId">The id of the marathon to remove.</param>
    /// <param name="requestedUserId">Optional Jellyfin user id for API-key requests.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{marathonId:guid}")]
    public ActionResult Delete(Guid marathonId, [FromQuery(Name = "userId")] Guid? requestedUserId = null)
    {
        var userId = ResolveUserId(requestedUserId);
        if (userId == Guid.Empty)
        {
            return Unauthorized();
        }

        _store.Write(data =>
        {
            MigrateLegacyState(data, userId);
            if (data.Marathons.TryGetValue(userId, out var entries))
            {
                if (entries.TryGetValue(marathonId, out var selected))
                {
                    var matchingIds = entries.Values
                        .Where(state => IsSameQueue(selected, state))
                        .Select(state => state.MarathonId)
                        .ToArray();
                    foreach (var matchingId in matchingIds)
                    {
                        entries.Remove(matchingId);
                    }
                }
                else
                {
                    entries.Remove(marathonId);
                }

                if (entries.Count == 0)
                {
                    data.Marathons.Remove(userId);
                }
            }

            return true;
        });
        return NoContent();
    }

    private static void MigrateLegacyState(StoreData data, Guid userId)
    {
        if (!data.ActiveMarathons.Remove(userId, out var legacy))
        {
            return;
        }

        if (!data.Marathons.TryGetValue(userId, out var entries))
        {
            entries = [];
            data.Marathons[userId] = entries;
        }

        legacy.MarathonId = legacy.MarathonId == Guid.Empty ? Guid.NewGuid() : legacy.MarathonId;
        entries[legacy.MarathonId] = legacy;
    }

    private static bool IsSameQueue(MarathonState left, MarathonState right) =>
        left.SourceId == right.SourceId &&
        string.Equals(left.SourceType, right.SourceType, StringComparison.OrdinalIgnoreCase) &&
        left.ItemIds.SequenceEqual(right.ItemIds);

    private Guid ResolveUserId(Guid? requestedUserId)
    {
        var authenticatedUserId = UserHelper.GetUserId(User);
        return authenticatedUserId != Guid.Empty ? authenticatedUserId : requestedUserId.GetValueOrDefault();
    }
}
