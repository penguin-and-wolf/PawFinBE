using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>Discovers and serves clips from a library item's Extras/Moments directory.</summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/Moments")]
[Produces("application/json")]
public class MomentsController : ControllerBase
{
    private static readonly Regex ClipOrder = new("S(?<season>\\d+)E(?<episode>\\d+)P(?<part>\\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly PawfinStore _store;
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly IDtoService _dtoService;

    /// <summary>Initializes a new instance of the <see cref="MomentsController"/> class.</summary>
    /// <param name="store">The Pawfin data store.</param>
    /// <param name="libraryManager">The Jellyfin library manager.</param>
    /// <param name="userManager">The Jellyfin user manager.</param>
    /// <param name="dtoService">The Jellyfin DTO service.</param>
    public MomentsController(PawfinStore store, ILibraryManager libraryManager, IUserManager userManager, IDtoService dtoService)
    {
        _store = store;
        _libraryManager = libraryManager;
        _userManager = userManager;
        _dtoService = dtoService;
    }

    /// <summary>Gets Moments groups for a movie or series the current user may access.</summary>
    /// <param name="itemId">The movie or series id.</param>
    /// <returns>The available groups and their ordered clips.</returns>
    [HttpGet("{itemId:guid}")]
    public ActionResult<IReadOnlyList<MomentGroupDto>> Get(Guid itemId)
    {
        var userId = UserHelper.GetUserId(User);
        var user = userId == Guid.Empty ? null : _userManager.GetUserById(userId);
        if (user is null)
        {
            return Unauthorized();
        }

        if (!_store.Read(data => data.MomentAllowedUsers.Contains(userId)))
        {
            return Forbid();
        }

        var parent = _libraryManager.GetItemById(itemId);
        if (parent is null || (parent is not MediaBrowser.Controller.Entities.TV.Series && parent.GetType().Name != "Movie"))
        {
            return NotFound();
        }

        var momentsRoot = FindMomentsRoot(parent.Path);
        if (momentsRoot is null)
        {
            return Ok(Array.Empty<MomentGroupDto>());
        }

        var options = new DtoOptions(true);
        var libraryClips = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = parent.Id, Recursive = true })
            .Where(item => item.MediaType == Jellyfin.Data.Enums.MediaType.Video)
            .ToList();
        if (!libraryClips.Any(item => IsWithin(item.Path, momentsRoot)))
        {
            libraryClips = _libraryManager.GetItemList(new InternalItemsQuery(user) { Recursive = true })
                .Where(item => item.MediaType == Jellyfin.Data.Enums.MediaType.Video && IsWithin(item.Path, momentsRoot))
                .ToList();
        }

        var groups = Directory.EnumerateDirectories(momentsRoot)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(groupPath =>
            {
                var clips = Directory.EnumerateDirectories(groupPath)
                    .SelectMany(clipPath => libraryClips.Where(item => IsWithin(item.Path, clipPath)))
                    .DistinctBy(item => item.Id)
                    .OrderBy(item => ParseOrder(item.Path ?? item.Name))
                    .ThenBy(item => Path.GetFileName(item.Path), StringComparer.OrdinalIgnoreCase)
                    .Select(item => _dtoService.GetBaseItemDto(item, options, user))
                    .ToList();
                return new MomentGroupDto
                {
                    Id = StableId(parent.Id, groupPath),
                    Name = Path.GetFileName(groupPath) ?? string.Empty,
                    MiniatureUrl = FindMiniature(groupPath) is null ? null : new Uri($"/Pawfin/Moments/{parent.Id}/Groups/{StableId(parent.Id, groupPath)}/Miniature", UriKind.Relative),
                    Clips = clips,
                };
            })
            .ToList();
        return Ok(groups);
    }

    /// <summary>Serves the optional Miniature image for a Moments group.</summary>
    /// <param name="itemId">The movie or series id.</param>
    /// <param name="groupId">The stable group id.</param>
    /// <returns>The image file.</returns>
    [HttpGet("{itemId:guid}/Groups/{groupId:guid}/Miniature")]
    public IActionResult GetMiniature(Guid itemId, Guid groupId)
    {
        var userId = UserHelper.GetUserId(User);
        if (userId == Guid.Empty || !_store.Read(data => data.MomentAllowedUsers.Contains(userId)))
        {
            return Forbid();
        }

        var parent = _libraryManager.GetItemById(itemId);
        var momentsRoot = parent is null ? null : FindMomentsRoot(parent.Path);
        var groupPath = momentsRoot is null ? null : Directory.EnumerateDirectories(momentsRoot)
            .FirstOrDefault(path => StableId(itemId, path) == groupId);
        var image = groupPath is null ? null : FindMiniature(groupPath);
        return image is null ? NotFound() : PhysicalFile(image, ContentTypeFor(image));
    }

    private static string? FindMomentsRoot(string? itemPath)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return null;
        }

        var directory = Directory.Exists(itemPath) ? itemPath : Path.GetDirectoryName(itemPath);
        if (directory is null || !Directory.Exists(directory))
        {
            return null;
        }

        var extras = Directory.EnumerateDirectories(directory)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Extras", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(path), "Extra's", StringComparison.OrdinalIgnoreCase));
        if (extras is null)
        {
            return null;
        }

        return Directory.EnumerateDirectories(extras)
            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), "Moments", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindMiniature(string directory) => Directory.EnumerateFiles(directory)
        .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), "Miniature", StringComparison.OrdinalIgnoreCase) &&
            ImageExtensions.Contains(Path.GetExtension(path)));

    private static bool IsWithin(string? itemPath, string directory) =>
        !string.IsNullOrWhiteSpace(itemPath) && Path.GetFullPath(itemPath).StartsWith(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory)) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static Guid StableId(Guid parentId, string path)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(parentId + "|" + Path.GetFullPath(path)));
        return new Guid(bytes.AsSpan(0, 16));
    }

    private static (int Season, int Episode, int Part) ParseOrder(string value)
    {
        var match = ClipOrder.Match(value);
        return match.Success
            ? (int.Parse(match.Groups["season"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["episode"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["part"].Value, CultureInfo.InvariantCulture))
            : (int.MaxValue, int.MaxValue, int.MaxValue);
    }

    private static string ContentTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
