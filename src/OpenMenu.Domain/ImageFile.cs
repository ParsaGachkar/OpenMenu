namespace OpenMenu.Domain;

/// <summary>
/// Image uploaded at runtime and stored in the database (small files).
/// Served back publicly via GET /images/{id}.
/// </summary>
public class ImageFile
{
    public Guid Id { get; set; }

    /// <summary>MIME type, restricted to image/* on upload.</summary>
    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];
}
