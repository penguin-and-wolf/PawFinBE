using System;
using System.IO;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using PenguinAndWolf.Pawfin.Model;

namespace PenguinAndWolf.Pawfin.Services;

/// <summary>
/// Server side persistence so data is shared by every device of a user.
/// </summary>
public class PawfinStore
{
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    private readonly object _lock = new();
    private readonly string _root;
    private readonly string _dataFile;
    private readonly StoreData _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="PawfinStore"/> class.
    /// </summary>
    /// <param name="paths">The application paths.</param>
    public PawfinStore(IApplicationPaths paths)
    {
        _root = Path.Combine(paths.DataPath, "pawfin");
        _dataFile = Path.Combine(_root, "data.json");
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        _data = Load();
    }

    /// <summary>
    /// Runs a read/modify operation under lock and saves afterwards.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The operation.</param>
    /// <returns>The operation result.</returns>
    public T Write<T>(Func<StoreData, T> action)
    {
        lock (_lock)
        {
            var result = action(_data);
            Save();
            return result;
        }
    }

    /// <summary>
    /// Runs a read operation under lock.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    /// <param name="action">The operation.</param>
    /// <returns>The operation result.</returns>
    public T Read<T>(Func<StoreData, T> action)
    {
        lock (_lock)
        {
            return action(_data);
        }
    }

    /// <summary>
    /// Gets the directory holding an owner's asset files.
    /// </summary>
    /// <param name="ownerId">The seasonal or collection id.</param>
    /// <returns>The directory path.</returns>
    public string GetAssetDirectory(Guid ownerId)
    {
        var dir = Path.Combine(_root, "assets", ownerId.ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Deletes an owner's asset directory.
    /// </summary>
    /// <param name="ownerId">The owner id.</param>
    public void DeleteAssetDirectory(Guid ownerId)
    {
        var dir = Path.Combine(_root, "assets", ownerId.ToString("N"));
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, true);
        }
    }

    private StoreData Load()
    {
        try
        {
            if (File.Exists(_dataFile))
            {
                return JsonSerializer.Deserialize<StoreData>(File.ReadAllText(_dataFile), _json) ?? new StoreData();
            }
        }
        catch (JsonException)
        {
            File.Copy(_dataFile, _dataFile + ".corrupt-" + DateTime.UtcNow.Ticks, true);
        }

        return new StoreData();
    }

    private void Save()
    {
        var tmp = _dataFile + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, _json));
        File.Move(tmp, _dataFile, true);
    }
}
