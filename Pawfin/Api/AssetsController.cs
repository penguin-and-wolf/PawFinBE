using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PenguinAndWolf.Pawfin.Model;
using PenguinAndWolf.Pawfin.Services;

namespace PenguinAndWolf.Pawfin.Api;

/// <summary>
/// Images, backgrounds and themes for seasonals and collections.
/// </summary>
[ApiController]
[TypeFilter(typeof(PawfinApiExceptionFilter))]
[Authorize]
[Route("Pawfin/{ownerType}/{ownerId}")]
public class AssetsController : ControllerBase
{
    private const long MaxUpload = 512L * 1024 * 1024;

    private static readonly string[] _imageTypes =
        ["Primary", "Logo", "Banner", "Thumb", "Art", "Disc", "Box", "BoxRear", "Menu", "Screenshot", "Chapter", "Profile"];

    private static readonly Dictionary<string, string> _extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["image/avif"] = ".avif",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp3"] = ".mp3",
        ["audio/flac"] = ".flac",
        ["audio/ogg"] = ".ogg",
        ["audio/wav"] = ".wav",
        ["audio/aac"] = ".aac",
        ["audio/mp4"] = ".m4a",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/x-matroska"] = ".mkv"
    };

    private readonly PawfinStore _store;
    private readonly ILibraryManager _libraryManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsController"/> class.
    /// </summary>
    /// <param name="store">The store.</param>
    /// <param name="libraryManager">The library manager.</param>
    public AssetsController(PawfinStore store, ILibraryManager libraryManager)
    {
        _store = store;
        _libraryManager = libraryManager;
    }

    /// <summary>
    /// Gets the assets of a seasonal or collection.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <returns>The assets.</returns>
    [HttpGet("Assets")]
    public ActionResult<AssetSet> GetAssets([FromRoute] string ownerType, [FromRoute] Guid ownerId)
    {
        var set = FindSet(ownerType, ownerId);
        return set is null ? NotFound() : set;
    }

    /// <summary>
    /// Uploads a single image (Primary, Logo, Banner, Thumb, Art, ...). The request body is the raw image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="imageType">The image type.</param>
    /// <returns>No content.</returns>
    [HttpPost("Images/{imageType}")]
    [RequestSizeLimit(MaxUpload)]
    public async Task<ActionResult> SetImage([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] string imageType)
    {
        var type = _imageTypes.FirstOrDefault(t => t.Equals(imageType, StringComparison.OrdinalIgnoreCase));
        if (type is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported image type", detail: "Use one of: " + string.Join(", ", _imageTypes));
        }

        return await Upload(ownerType, ownerId, "image/", (set, file) =>
        {
            if (set.Images.TryGetValue(type, out var old))
            {
                DeleteFile(ownerId, old);
            }

            set.Images[type] = file;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a single image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="imageType">The image type.</param>
    /// <returns>The image.</returns>
    [HttpGet("Images/{imageType}")]
    [AllowAnonymous]
    public ActionResult GetImage([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] string imageType)
    {
        var set = FindSet(ownerType, ownerId);
        return set is not null && set.Images.TryGetValue(imageType, out var file) ? Serve(ownerId, file) : NotFound();
    }

    /// <summary>
    /// Deletes a single image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="imageType">The image type.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Images/{imageType}")]
    public ActionResult DeleteImage([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] string imageType)
    {
        return Modify(ownerType, ownerId, set =>
        {
            if (!set.Images.Remove(imageType, out var file))
            {
                return false;
            }

            DeleteFile(ownerId, file);
            return true;
        });
    }

    /// <summary>
    /// Adds a background image. The request body is the raw image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <returns>No content.</returns>
    [HttpPost("Backdrops")]
    [RequestSizeLimit(MaxUpload)]
    public Task<ActionResult> AddBackdrop([FromRoute] string ownerType, [FromRoute] Guid ownerId)
        => Upload(ownerType, ownerId, "image/", (set, file) => set.Backdrops.Add(file));

    /// <summary>
    /// Gets a background image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The backdrop index.</param>
    /// <returns>The image.</returns>
    [HttpGet("Backdrops/{index}")]
    [AllowAnonymous]
    public ActionResult GetBackdrop([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => ServeIndexed(ownerType, ownerId, s => s.Backdrops, index);

    /// <summary>
    /// Deletes a background image.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The backdrop index.</param>
    /// <returns>No content.</returns>
    [HttpDelete("Backdrops/{index}")]
    public ActionResult DeleteBackdrop([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => DeleteIndexed(ownerType, ownerId, s => s.Backdrops, index);

    /// <summary>
    /// Adds a theme song. The request body is the raw audio.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <returns>No content.</returns>
    [HttpPost("ThemeSongs")]
    [RequestSizeLimit(MaxUpload)]
    public Task<ActionResult> AddThemeSong([FromRoute] string ownerType, [FromRoute] Guid ownerId)
        => Upload(ownerType, ownerId, "audio/", (set, file) => set.ThemeSongs.Add(file));

    /// <summary>
    /// Gets a theme song.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The theme index.</param>
    /// <returns>The audio.</returns>
    [HttpGet("ThemeSongs/{index}")]
    public ActionResult GetThemeSong([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => ServeIndexed(ownerType, ownerId, s => s.ThemeSongs, index);

    /// <summary>
    /// Deletes a theme song.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The theme index.</param>
    /// <returns>No content.</returns>
    [HttpDelete("ThemeSongs/{index}")]
    public ActionResult DeleteThemeSong([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => DeleteIndexed(ownerType, ownerId, s => s.ThemeSongs, index);

    /// <summary>
    /// Adds a theme video. The request body is the raw video.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <returns>No content.</returns>
    [HttpPost("ThemeVideos")]
    [RequestSizeLimit(MaxUpload)]
    public Task<ActionResult> AddThemeVideo([FromRoute] string ownerType, [FromRoute] Guid ownerId)
        => Upload(ownerType, ownerId, "video/", (set, file) => set.ThemeVideos.Add(file));

    /// <summary>
    /// Gets a theme video.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The theme index.</param>
    /// <returns>The video.</returns>
    [HttpGet("ThemeVideos/{index}")]
    public ActionResult GetThemeVideo([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => ServeIndexed(ownerType, ownerId, s => s.ThemeVideos, index);

    /// <summary>
    /// Deletes a theme video.
    /// </summary>
    /// <param name="ownerType">Seasonals or Collections.</param>
    /// <param name="ownerId">The owner id.</param>
    /// <param name="index">The theme index.</param>
    /// <returns>No content.</returns>
    [HttpDelete("ThemeVideos/{index}")]
    public ActionResult DeleteThemeVideo([FromRoute] string ownerType, [FromRoute] Guid ownerId, [FromRoute] int index)
        => DeleteIndexed(ownerType, ownerId, s => s.ThemeVideos, index);

    private AssetSet? FindSet(string ownerType, Guid ownerId)
    {
        if (ownerType.Equals("Seasonals", StringComparison.OrdinalIgnoreCase))
        {
            return _store.Read(d => d.Seasonals.FirstOrDefault(s => s.Id == ownerId)?.Assets);
        }

        if (ownerType.Equals("Collections", StringComparison.OrdinalIgnoreCase) && _libraryManager.GetItemById(ownerId) is BoxSet)
        {
            return _store.Read(d => d.CollectionAssets.TryGetValue(ownerId, out var set) ? set : new AssetSet());
        }

        return null;
    }

    private async Task<ActionResult> Upload(string ownerType, Guid ownerId, string contentTypePrefix, Action<AssetSet, AssetFile> attach)
    {
        var contentType = (Request.ContentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (!contentType.StartsWith(contentTypePrefix, StringComparison.Ordinal))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unsupported asset type", detail: "Content-Type must start with " + contentTypePrefix);
        }

        if (FindSet(ownerType, ownerId) is null)
        {
            return NotFound();
        }

        var ext = _extensions.TryGetValue(contentType, out var e) ? e : ".bin";
        var file = new AssetFile { FileName = Guid.NewGuid().ToString("N") + ext, ContentType = contentType };
        var path = Path.Combine(_store.GetAssetDirectory(ownerId), file.FileName);
        var fs = System.IO.File.Create(path);
        await using (fs.ConfigureAwait(false))
        {
            await Request.Body.CopyToAsync(fs).ConfigureAwait(false);
        }

        var result = Modify(ownerType, ownerId, set =>
        {
            attach(set, file);
            return true;
        });

        if (result is NotFoundResult)
        {
            System.IO.File.Delete(path);
        }

        return result;
    }

    private ActionResult Modify(string ownerType, Guid ownerId, Func<AssetSet, bool> action)
    {
        if (ownerType.Equals("Seasonals", StringComparison.OrdinalIgnoreCase))
        {
            var found = _store.Write(d =>
            {
                var s = d.Seasonals.FirstOrDefault(x => x.Id == ownerId);
                return s is not null && action(s.Assets);
            });
            return found ? NoContent() : NotFound();
        }

        if (ownerType.Equals("Collections", StringComparison.OrdinalIgnoreCase) && _libraryManager.GetItemById(ownerId) is BoxSet)
        {
            var found = _store.Write(d =>
            {
                if (!d.CollectionAssets.TryGetValue(ownerId, out var set))
                {
                    set = new AssetSet();
                    d.CollectionAssets[ownerId] = set;
                }

                return action(set);
            });
            return found ? NoContent() : NotFound();
        }

        return NotFound();
    }

    private ActionResult ServeIndexed(string ownerType, Guid ownerId, Func<AssetSet, List<AssetFile>> list, int index)
    {
        var set = FindSet(ownerType, ownerId);
        if (set is null)
        {
            return NotFound();
        }

        var files = _store.Read(_ => list(set));
        return index >= 0 && index < files.Count ? Serve(ownerId, files[index]) : NotFound();
    }

    private ActionResult DeleteIndexed(string ownerType, Guid ownerId, Func<AssetSet, List<AssetFile>> list, int index)
    {
        return Modify(ownerType, ownerId, set =>
        {
            var files = list(set);
            if (index < 0 || index >= files.Count)
            {
                return false;
            }

            DeleteFile(ownerId, files[index]);
            files.RemoveAt(index);
            return true;
        });
    }

    private ActionResult Serve(Guid ownerId, AssetFile file)
    {
        var path = Path.Combine(_store.GetAssetDirectory(ownerId), Path.GetFileName(file.FileName));
        return System.IO.File.Exists(path) ? PhysicalFile(path, file.ContentType, true) : NotFound();
    }

    private void DeleteFile(Guid ownerId, AssetFile file)
    {
        var path = Path.Combine(_store.GetAssetDirectory(ownerId), Path.GetFileName(file.FileName));
        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }
    }
}
