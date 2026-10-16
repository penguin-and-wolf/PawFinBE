using System;
using System.Collections.Generic;
using System.IO;
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
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".ts", ".webm", ".wmv",
    };

    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IUserDataManager _userDataManager;
    private readonly IDtoService _dtoService;
    private readonly PawfinStore _store;

    /// <summary>Initializes a new instance of the <see cref="ContinueWatchingController"/> class.</summary>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="userDataManager">The Jellyfin user data manager.</param>
    /// <param name="dtoService">The Jellyfin DTO service.</param>
    /// <param name="store">The Pawfin data store.</param>
    public ContinueWatchingController(ILibraryManager libraryManager, IUserManager userManager, IUserDataManager userDataManager, IDtoService dtoService, PawfinStore store)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _userDataManager = userDataManager;
        _dtoService = dtoService;
        _store = store;
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
        var resumableItems = _libraryManager.GetItemList(query)
            .Where(item => item.MediaType == MediaType.Video)
            .ToDictionary(item => item.Id);

        // Moment clips are deliberately outside Jellyfin's normal Extras/library
        // hierarchy. Discover them from visible movie/series folders so their own
        // playback positions can still participate in Pawfin's Continue Watching row.
        var momentPairings = new Dictionary<Guid, string>();
        if (_store.Read(data => data.MomentAllowedUsers.Contains(userId)))
        {
            var visibleItems = _libraryManager.GetItemList(new InternalItemsQuery(user) { Recursive = true });
            foreach (var parent in visibleItems.Where(item => item.GetType().Name is "Movie" or "Series"))
            {
                var momentsRoot = FindMomentsRoot(parent.Path);
                if (momentsRoot is null)
                {
                    continue;
                }

                foreach (var path in Directory.EnumerateFiles(momentsRoot, "*", SearchOption.AllDirectories)
                    .Where(path => VideoExtensions.Contains(Path.GetExtension(path))))
                {
                    var clip = _libraryManager.FindByPath(path, false);
                    if (clip is not null)
                    {
                        var relative = Path.GetRelativePath(momentsRoot, path);
                        var groupName = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                        momentPairings[clip.Id] = $"{parent.Id:D}:{groupName}";
                        resumableItems.TryAdd(clip.Id, clip);
                    }
                }
            }
        }

        var resumable = resumableItems.Values
            .Select(item => new { Item = item, UserData = _userDataManager.GetUserData(user, item) })
            .Where(entry => entry.UserData is { PlaybackPositionTicks: > 0, Played: false })
            .OrderByDescending(entry => entry.UserData?.LastPlayedDate)
            .GroupBy(entry => momentPairings.TryGetValue(entry.Item.Id, out var pairing)
                ? $"moment:{pairing}"
                : $"item:{entry.Item.Id:D}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(entry => entry.UserData?.LastPlayedDate)
            .Select(entry => entry.Item)
            .ToList();
        var page = resumable.Skip(offset).Take(Math.Clamp(limit, 0, 500))
            .Select(item =>
            {
                var dto = _dtoService.GetBaseItemDto(item, options, user);
                if (momentPairings.TryGetValue(item.Id, out var pairing))
                {
                    dto.Tags = (dto.Tags ?? Array.Empty<string>())
                        .Append("PawfinMoment")
                        .Append($"PawfinMomentGroup:{pairing}")
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }

                return dto;
            }).ToList();
        return new QueryResult<BaseItemDto>(resumable.Count, offset, page);
    }

    private static string? FindMomentsRoot(string? itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath)) return null;
        var directory = Directory.Exists(itemPath) ? itemPath : Path.GetDirectoryName(itemPath);
        if (directory is null || !Directory.Exists(directory)) return null;
        var extras = Directory.EnumerateDirectories(directory).FirstOrDefault(path =>
            string.Equals(Path.GetFileName(path), "Extras", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileName(path), "Extra's", StringComparison.OrdinalIgnoreCase));
        return extras is null ? null : Directory.EnumerateDirectories(extras)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Moments", StringComparison.OrdinalIgnoreCase));
    }
}
