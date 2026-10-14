using System;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>
/// Manages seasonals.
/// </summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/Seasonals")]
[Produces("application/json")]
public class SeasonalsController : ControllerBase
{
    private static readonly Action<ILogger, string, Exception?> _logInvalidSeasonalId =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1, nameof(GetSeasonal)),
            "Pawfin seasonal request used invalid id {SeasonalId}");

    private static readonly Action<ILogger, Guid, Exception?> _logSeasonalUserUnavailable =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2, nameof(ResolveItems)),
            "Pawfin seasonal {SeasonalId} items could not be resolved because the authenticated Jellyfin user id is unavailable");

    private readonly PawfinStore _store;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IDtoService _dtoService;
    private readonly ILogger<SeasonalsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SeasonalsController"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dtoService">The dto service.</param>
    /// <param name="logger">The logger.</param>
    public SeasonalsController(
        PawfinStore store,
        ILibraryManager libraryManager,
        IUserManager userManager,
        IDtoService dtoService,
        ILogger<SeasonalsController> logger)
    {
        _store = store;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _dtoService = dtoService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all seasonals.
    /// </summary>
    /// <param name="activeOnly">Only return seasonals active today (or on <paramref name="date"/>).</param>
    /// <param name="date">Day used for the active check. Defaults to today.</param>
    /// <param name="includeItems">Resolve the ordered items.</param>
    /// <returns>The seasonals.</returns>
    [HttpGet]
    public ActionResult<IReadOnlyList<SeasonalDto>> GetSeasonals(
        [FromQuery] bool activeOnly = false,
        [FromQuery] DateTime? date = null,
        [FromQuery] bool includeItems = false)
    {
        var day = date ?? DateTime.Now;
        var seasonals = _store.Read(d => d.Seasonals.ToList());
        var result = seasonals
            .Where(s => !activeOnly || s.IsActiveOn(day))
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => ToDto(s, day, includeItems))
            .ToList();
        return result;
    }

    /// <summary>
    /// Gets a seasonal with its ordered items.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <param name="includeItems">Resolve the ordered items.</param>
    /// <returns>The seasonal.</returns>
    [HttpGet("{id}")]
    public ActionResult<SeasonalDto> GetSeasonal([FromRoute] string id, [FromQuery] bool includeItems = true)
    {
        if (!Guid.TryParseExact(id, "N", out var seasonalId)
            && !Guid.TryParseExact(id, "D", out seasonalId))
        {
            _logInvalidSeasonalId(_logger, id, null);
            return BadRequest("The seasonal id must be a valid GUID.");
        }

        var seasonal = Find(seasonalId);
        return seasonal is null ? NotFound() : ToDto(seasonal, DateTime.Now, includeItems);
    }

    /// <summary>
    /// Creates a seasonal.
    /// </summary>
    /// <param name="request">The seasonal.</param>
    /// <returns>The created seasonal.</returns>
    [HttpPost]
    public ActionResult<SeasonalDto> Create([FromBody, BindRequired] SeasonalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid seasonal", detail: "Name is required.");
        }

        var seasonal = new Seasonal { Name = request.Name.Trim() };
        Apply(seasonal, request);
        if (request.ItemIds is not null)
        {
            foreach (var itemId in request.ItemIds.Distinct())
            {
                seasonal.Items.Add(new SeasonalItem { ItemId = itemId, Order = seasonal.Items.Count });
            }
        }

        _store.Write(d =>
        {
            d.Seasonals.Add(seasonal);
            return true;
        });
        return CreatedAtAction(nameof(GetSeasonal), new { id = seasonal.Id }, ToDto(seasonal, DateTime.Now, false));
    }

    /// <summary>
    /// Updates a seasonal.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <param name="request">The changes.</param>
    /// <returns>The updated seasonal.</returns>
    [HttpPut("{id}")]
    public ActionResult<SeasonalDto> Update([FromRoute] Guid id, [FromBody, BindRequired] SeasonalRequest request)
    {
        var updated = _store.Write(d =>
        {
            var s = d.Seasonals.FirstOrDefault(x => x.Id == id);
            if (s is not null)
            {
                if (!string.IsNullOrWhiteSpace(request.Name))
                {
                    s.Name = request.Name.Trim();
                }

                Apply(s, request);
            }

            return s;
        });
        return updated is null ? NotFound() : ToDto(updated, DateTime.Now, false);
    }

    /// <summary>
    /// Deletes a seasonal and its stored images/themes.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    public ActionResult Delete([FromRoute] Guid id)
    {
        var removed = _store.Write(d => d.Seasonals.RemoveAll(x => x.Id == id) > 0);
        if (!removed)
        {
            return NotFound();
        }

        _store.DeleteAssetDirectory(id);
        return NoContent();
    }

    /// <summary>
    /// Adds items (movies, series, episodes, collections) to a seasonal.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <param name="itemIds">The item ids.</param>
    /// <returns>The seasonal.</returns>
    [HttpPost("{id}/Items")]
    public ActionResult<SeasonalDto> AddItems([FromRoute] Guid id, [FromQuery, BindRequired] Guid[] itemIds)
    {
        if (itemIds.Any(i => _libraryManager.GetItemById(i) is null))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid seasonal items", detail: "One or more item IDs do not exist in the Jellyfin library.");
        }

        var seasonal = _store.Write(d =>
        {
            var s = d.Seasonals.FirstOrDefault(x => x.Id == id);
            if (s is not null)
            {
                foreach (var itemId in itemIds.Distinct().Where(i => s.Items.All(e => e.ItemId != i)))
                {
                    s.Items.Add(new SeasonalItem { ItemId = itemId, Order = s.Items.Count });
                }
            }

            return s;
        });
        return seasonal is null ? NotFound() : ToDto(seasonal, DateTime.Now, false);
    }

    /// <summary>
    /// Removes items from a seasonal.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <param name="itemIds">The item ids.</param>
    /// <returns>The seasonal.</returns>
    [HttpDelete("{id}/Items")]
    public ActionResult<SeasonalDto> RemoveItems([FromRoute] Guid id, [FromQuery, BindRequired] Guid[] itemIds)
    {
        var seasonal = _store.Write(d =>
        {
            var s = d.Seasonals.FirstOrDefault(x => x.Id == id);
            if (s is not null)
            {
                s.Items.RemoveAll(e => itemIds.Contains(e.ItemId));
                Renumber(s);
            }

            return s;
        });
        return seasonal is null ? NotFound() : ToDto(seasonal, DateTime.Now, false);
    }

    /// <summary>
    /// Sets the explicit order of the items. Listed ids come first, the rest keep their relative order.
    /// </summary>
    /// <param name="id">The seasonal id.</param>
    /// <param name="itemIds">The ids in desired order.</param>
    /// <returns>The seasonal.</returns>
    [HttpPost("{id}/Items/Order")]
    public ActionResult<SeasonalDto> SetOrder([FromRoute] Guid id, [FromBody, BindRequired] Guid[] itemIds)
    {
        var seasonal = _store.Write(d =>
        {
            var s = d.Seasonals.FirstOrDefault(x => x.Id == id);
            if (s is not null)
            {
                var rank = itemIds.Select((v, i) => (v, i)).GroupBy(t => t.v).ToDictionary(g => g.Key, g => g.First().i);
                s.Items = s.Items
                    .OrderBy(e => rank.TryGetValue(e.ItemId, out var r) ? r : int.MaxValue)
                    .ThenBy(e => e.Order)
                    .ToList();
                Renumber(s);
            }

            return s;
        });
        return seasonal is null ? NotFound() : ToDto(seasonal, DateTime.Now, false);
    }

    private static void Renumber(Seasonal s)
    {
        for (var i = 0; i < s.Items.Count; i++)
        {
            s.Items[i].Order = i;
        }
    }

    private static void Apply(Seasonal s, SeasonalRequest r)
    {
        if (r.Overview is not null)
        {
            s.Overview = r.Overview;
        }

        if (r.ClearDates)
        {
            s.StartDate = null;
            s.EndDate = null;
        }

        if (r.StartDate is not null)
        {
            s.StartDate = r.StartDate.Value.Date;
        }

        if (r.EndDate is not null)
        {
            s.EndDate = r.EndDate.Value.Date;
        }

        if (r.Recurring is not null)
        {
            s.Recurring = r.Recurring.Value;
        }

        if (r.OrderMode is not null)
        {
            s.OrderMode = r.OrderMode.Value;
        }
    }

    private Seasonal? Find(Guid id) => _store.Read(d => d.Seasonals.FirstOrDefault(x => x.Id == id));

    private SeasonalDto ToDto(Seasonal s, DateTime day, bool includeItems)
    {
        return _store.Read(_ => new SeasonalDto
        {
            Id = s.Id,
            Name = s.Name,
            Overview = s.Overview,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            Recurring = s.Recurring,
            IsActive = s.IsActiveOn(day),
            OrderMode = s.OrderMode,
            ItemCount = s.Items.Count,
            Assets = s.Assets,
            Items = includeItems ? ResolveItems(s) : null
        });
    }

    private List<MediaBrowser.Model.Dto.BaseItemDto> ResolveItems(Seasonal s)
    {
        var userId = UserHelper.GetUserId(User);
        var user = userId == Guid.Empty ? null : _userManager.GetUserById(userId);
        if (user is null)
        {
            _logSeasonalUserUnavailable(_logger, s.Id, null);
            return new List<MediaBrowser.Model.Dto.BaseItemDto>();
        }

        var items = s.Items
            .OrderBy(e => e.Order)
            .Select(e => _libraryManager.GetItemById(e.ItemId))
            .Where(i => i is not null && i.IsVisible(user))
            .Select(i => i!)
            .ToList();

        IEnumerable<MediaBrowser.Controller.Entities.BaseItem> ordered = s.OrderMode switch
        {
            SeasonalOrderMode.Random => Shuffle(items),
            SeasonalOrderMode.Chronological => items.OrderBy(i => i.PremiereDate ?? DateTime.MaxValue),
            SeasonalOrderMode.ReverseChronological => items.OrderByDescending(i => i.PremiereDate ?? DateTime.MinValue),
            SeasonalOrderMode.Alphabetical => items.OrderBy(i => i.SortName, StringComparer.OrdinalIgnoreCase),
            SeasonalOrderMode.DateAdded => items.OrderByDescending(i => i.DateCreated),
            _ => items
        };

        var options = new DtoOptions(true);
        return ordered.Select(i => _dtoService.GetBaseItemDto(i, options, user)).ToList();
    }

    private static List<T> Shuffle<T>(List<T> list)
    {
        var copy = list.ToList();
        for (var i = copy.Count - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }
}
