using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Paramore.Brighter.Observability;

namespace Paramore.Brighter.Transforms.Storage;

/// <summary>
/// Implements a file system-based storage provider for the Brighter Claim Check pattern.
/// This class handles the storing, retrieving, deleting, and checking existence of
/// message payloads (luggage) directly on the local file system.
/// </summary>
/// <remarks>
/// This store is suitable for scenarios where a shared network file system is accessible
/// to all instances of the application, or for simpler, single-instance deployments.
/// It provides both synchronous and asynchronous operations, conforming to Brighter's
/// <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/> interfaces.
/// <para>
/// **Configuration:**
/// Instances of this class are configured via <see cref="FileSystemOptions"/>, which
/// primarily specifies the base directory path where claim check payloads will be stored.
/// </para>
/// <para>
/// **Payload Handling:**
/// Each message payload (luggage) is stored as a separate file within the configured
/// directory, with the claim check identifier typically serving as the filename.
/// </para>
/// <para>
/// **Important Considerations:**
/// <list type="bullet">
///     <item>
///         <term>Concurrency</term>
///         <description>Care must be taken in highly concurrent environments to avoid file access conflicts.</description>
///     </item>
///     <item>
///         <term>Scalability</term>
///         <description>Less scalable than cloud-based object storage solutions for large-scale distributed systems.</description>
///     </item>
///     <item>
///         <term>Durability & Availability</term>
///         <description>Dependent on the underlying file system's resilience and availability.</description>
///     </item>
///     <item>
///         <term>Permissions</term>
///         <description>The application process must have appropriate read/write permissions to the specified directory.</description>
///     </item>
/// </list>
/// </para>
/// </remarks>
public class FileSystemStorageProvider : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "file_system";

    private readonly FileSystemOptions _options;
    private readonly Dictionary<string, string> _spanAttributes = new();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemStorageProvider"/> class
    /// with the specified file system storage options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="FileSystemOptions"/> containing the base directory path
    /// for storing claim check payloads.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the <see cref="FileSystemOptions.Path"/> property in <paramref name="options"/>
    /// is <see langword="null"/> or empty.
    /// </exception>
    public FileSystemStorageProvider(FileSystemOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrEmpty(options.Path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(options));
        }
    }
    
    /// <inheritdoc cref="IAmAStorageProvider.Tracer"/>
    public IAmABrighterTracer? Tracer { get; set; }
    
    /// <inheritdoc />
    public Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        EnsureStoreExists();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));
        try
        {
            Delete(claimCheck);
            return Task.CompletedTask;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));
        try
        {
            var memory = new MemoryStream();

#if NETSTANDARD 
            using var fs = File.OpenRead(Path.Combine(_options.Path, claimCheck));
            await fs.CopyToAsync(memory);
#else
            await using var fs = File.OpenRead(Path.Combine(_options.Path, claimCheck));
            await fs.CopyToAsync(memory, cancellationToken);
#endif
            
            await fs.FlushAsync(cancellationToken);
            memory.Position = 0;
            
            return memory;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));
        try
        {
            return Task.FromResult(File.Exists(Path.Combine(_options.Path, claimCheck)));
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var claimCheck = Uuid.NewAsString();
        
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes, stream.Length));
        try
        {
            
#if NETSTANDARD
            using var fs = File.Create(Path.Combine(_options.Path, claimCheck));
            await stream.CopyToAsync(fs);
#else
            await using var fs = File.Create(Path.Combine(_options.Path, claimCheck));
            await stream.CopyToAsync(fs, cancellationToken);
#endif
            
            await fs.FlushAsync(cancellationToken);
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void EnsureStoreExists()
    {
        if (_options.Strategy == StorageStrategy.Assume)
        {
            return;
        }

        if (Directory.Exists(_options.Path))
        {
            return;
        }

        if (_options.Strategy == StorageStrategy.Validate)
        {
            throw new InvalidOperationException($"There was no luggage store with the path: '{_options.Path}'");
        }
        
        Directory.CreateDirectory(_options.Path);
    }

    /// <inheritdoc />
    public void Delete(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));

        try
        {
            File.Delete(Path.Combine(_options.Path, claimCheck));
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Stream Retrieve(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));
        try
        {
            var memory = new MemoryStream();
            using var fs = File.OpenRead(Path.Combine(_options.Path, claimCheck));
            fs.CopyTo(memory);
            fs.Flush();
            memory.Position = 0;
            return memory;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public bool HasClaim(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes));
        try
        {
            return File.Exists(Path.Combine(_options.Path, claimCheck));
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public string Store(Stream stream)
    {
        var claimCheck = Uuid.NewAsString();
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, _options.Path, claimCheck, _spanAttributes, stream.Length));
        try
        {
            using var fs = File.Create(Path.Combine(_options.Path, claimCheck));
            stream.CopyTo(fs);
            fs.Flush();
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}
