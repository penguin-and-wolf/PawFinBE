namespace PenguinAndWolf.Pawfin.Model;

/// <summary>
/// A stored asset file.
/// </summary>
public class AssetFile
{
    /// <summary>Gets or sets the stored file name.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the content type.</summary>
    public string ContentType { get; set; } = "application/octet-stream";
}
