namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Provides configuration options for the <see cref="FileSystemStorageProvider"/>.
/// These options specify the base directory path where claim check payloads will be stored
/// when using a local or networked file system.
/// </summary>
/// <remarks>
/// This class extends <see cref="StorageOptions"/> to include settings specific
/// to file system-based storage. The primary configuration required is the <see cref="Path"/>
/// property, which dictates the root directory for all claim check operations.
/// </remarks>
public class FileSystemOptions(string path) : StorageOptions
{
    /// <summary>
    /// Gets or sets the absolute or relative path to the directory where claim check payloads
    /// (luggage) will be stored.
    /// </summary>
    /// <value>
    /// The file system path. This property is required and cannot be null or empty.
    /// </value>
    /// <example>
    /// <c>c:\ClaimCheckData</c> on Windows, or <c>/var/lib/claimcheck</c> on Linux/macOS.
    /// </example>
    public string Path { get; set; } = path;
}
