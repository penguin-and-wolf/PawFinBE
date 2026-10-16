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
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".webp" };
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".avi", ".m4v", ".mkv", ".mov", ".mp4", ".mpeg", ".mpg", ".ts", ".webm", ".wmv" };
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
        var groups = Directory.EnumerateDirectories(momentsRoot)
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(groupPath =>
            {
                var clipEntries = Directory.EnumerateFiles(groupPath, "*", SearchOption.AllDirectories)
                    .Where(path => VideoExtensions.Contains(Path.GetExtension(path)))
                    .Select(path => new { Path = path, Video = GetOrCreateMomentVideo(path) })
                    .DistinctBy(entry => entry.Video.Id)
                    .OrderBy(entry => ParseOrder(entry.Path))
                    .ThenBy(entry => Path.GetFileName(entry.Path), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new MomentGroupDto
                {
                    Id = StableId(parent.Id, groupPath),
                    Name = Path.GetFileName(groupPath) ?? string.Empty,
                    MiniatureUrl = FindMiniature(groupPath) is null ? null : new Uri($"/Pawfin/Moments/{parent.Id}/Groups/{StableId(parent.Id, groupPath)}/Miniature", UriKind.Relative),
                    PrimaryUrl = FindPrimary(groupPath) is null ? null : new Uri($"/Pawfin/Moments/{parent.Id}/Groups/{StableId(parent.Id, groupPath)}/Primary", UriKind.Relative),
                    ClipImageUrls = clipEntries
                        .Where(entry => FindAnyImage(Path.GetDirectoryName(entry.Path)) is not null)
                        .ToDictionary(
                            entry => entry.Video.Id.ToString("D", CultureInfo.InvariantCulture),
                            entry => new Uri($"/Pawfin/Moments/{parent.Id}/Clips/{entry.Video.Id}/Image", UriKind.Relative),
                            StringComparer.OrdinalIgnoreCase),
                    Clips = clipEntries.Select(entry => _dtoService.GetBaseItemDto(entry.Video, options, user)).ToList(),
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
        var image = groupPath is null ? null : FindMiniature(groupPath) ?? FindPrimary(groupPath) ?? FindAnyDirectImage(groupPath);
        return image is null ? NotFound() : PhysicalFile(image, ContentTypeFor(image));
    }

    /// <summary>Serves the Primary image stored directly in a Moments group folder.</summary>
    /// <param name="itemId">The movie or series id.</param>
    /// <param name="groupId">The stable group id.</param>
    /// <returns>The group's Primary image.</returns>
    [HttpGet("{itemId:guid}/Groups/{groupId:guid}/Primary")]
    public IActionResult GetPrimary(Guid itemId, Guid groupId)
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
        var image = groupPath is null ? null : FindPrimary(groupPath);
        return image is null ? NotFound() : PhysicalFile(image, ContentTypeFor(image));
    }

    /// <summary>Serves an image stored beside a Moments clip.</summary>
    /// <param name="itemId">The parent movie or series id.</param>
    /// <param name="clipId">The Jellyfin clip id.</param>
    /// <returns>The clip image.</returns>
    [HttpGet("{itemId:guid}/Clips/{clipId:guid}/Image")]
    public IActionResult GetClipImage(Guid itemId, Guid clipId)
    {
        var userId = UserHelper.GetUserId(User);
        if (userId == Guid.Empty || !_store.Read(data => data.MomentAllowedUsers.Contains(userId)))
        {
            return Forbid();
        }

        var parent = _libraryManager.GetItemById(itemId);
        var momentsRoot = parent is null ? null : FindMomentsRoot(parent.Path);
        if (momentsRoot is null)
        {
            return NotFound();
        }

        var clip = _libraryManager.GetItemById(clipId);
        var clipPath = clip?.Path is not null && IsWithin(clip.Path, momentsRoot)
            ? clip.Path
            : Directory.EnumerateFiles(momentsRoot, "*", SearchOption.AllDirectories)
                .Where(path => VideoExtensions.Contains(Path.GetExtension(path)))
                // Use the same lookup/creation path as group discovery. Existing
                // library items can have an ID that differs from GetNewItemId().
                .FirstOrDefault(path => GetOrCreateMomentVideo(path).Id == clipId);
        var image = clipPath is null ? null : FindAnyImage(Path.GetDirectoryName(clipPath));
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

    private BaseItem GetOrCreateMomentVideo(string path)
    {
        var existing = _libraryManager.FindByPath(path, false);
        if (existing is not null)
        {
            return existing;
        }

        var file = new FileInfo(path);
        var id = _libraryManager.GetNewItemId(file.FullName, typeof(Video));
        var video = new Video
        {
            Id = id,
            Name = Path.GetFileNameWithoutExtension(file.Name),
            Path = file.FullName,
            DateCreated = file.CreationTimeUtc,
            DateModified = file.LastWriteTimeUtc,
        };
        _libraryManager.CreateItem(video, null);
        return _libraryManager.GetItemById(id) ?? video;
    }

    private static string? FindMiniature(string directory) => FindNamedImage(directory, "Miniature") ??
        FindNamedImage(directory, "Minature");

    private static string? FindPrimary(string directory) => FindNamedImage(directory, "Primary");

    private static string? FindNamedImage(string directory, string name)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }

        return Directory.EnumerateFiles(directory)
            .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
            .FirstOrDefault(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindAnyImage(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        var files = GetDirectImages(directory);
        var preferred = files.FirstOrDefault(IsMiniature);
        if (preferred is not null)
        {
            return preferred;
        }

        if (files.Count > 0)
        {
            return files.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).First();
        }

        return Directory.EnumerateDirectories(directory)
            .OrderBy(path => ParseOrder(Path.GetFileName(path) ?? string.Empty))
            .Select(FindAnyImage)
            .FirstOrDefault(path => path is not null);
    }

    private static string? FindAnyDirectImage(string directory) => GetDirectImages(directory)
        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();

    private static List<string> GetDirectImages(string directory) => Directory.EnumerateFiles(directory)
        .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
        .ToList();

    private static bool IsMiniature(string path) =>
        (string.Equals(Path.GetFileNameWithoutExtension(path), "Miniature", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFileNameWithoutExtension(path), "Minature", StringComparison.OrdinalIgnoreCase)) &&
        ImageExtensions.Contains(Path.GetExtension(path));

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
        ".bmp" => "image/bmp",
        ".gif" => "image/gif",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };
}
