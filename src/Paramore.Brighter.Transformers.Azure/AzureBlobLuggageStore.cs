using Azure.Core;
using Azure.Storage.Blobs;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.Azure;

/// <summary>
/// Implements a storage provider for the Brighter Claim Check pattern using Azure Blob Storage.
/// This class handles the storing, retrieving, deleting, and checking existence of
/// message payloads (luggage) in an Azure Blob container.
/// </summary>
/// <remarks>
/// This store leverages Azure Blob Storage's robust and scalable object storage capabilities
/// to manage large message payloads efficiently and reliably. It provides both synchronous
/// and asynchronous operations, conforming to Brighter's <see cref="IAmAStorageProvider"/>
/// and <see cref="IAmAStorageProviderAsync"/> interfaces.
/// <para>
/// **Configuration:**
/// Instances of this class are configured via <see cref="AzureBlobLuggageOptions"/>, which
/// encapsulates the necessary connection details (e.g., connection string, container URI,
/// and credentials). The constructor supports two primary ways to initialize the
/// <see cref="BlobContainerClient"/>:
/// <list type="bullet">
///     <item>
///         <term>URI and Credential</term>
///         <description>Recommended for production environments using Azure Identity (e.g., <see cref="TokenCredential"/>).</description>
///     </item>
///     <item>
///         <term>Connection String and Container Name</term>
///         <description>Suitable for development and testing, or scenarios where a connection string is directly used.</description>
///     </item>
/// </list>
/// </para>
/// <para>
/// **Payload Handling:**
/// Each message payload (luggage) is stored as a block blob within the specified Azure Blob container.
/// </para>
/// </remarks>
public class AzureBlobLuggageStore : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "azure_blob";
    private readonly Dictionary<string, string> _spanAttributes = new();
    private readonly BlobContainerClient _blobClient;
    private readonly AzureBlobLuggageOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureBlobLuggageStore"/> class
    /// with the specified Azure Blob storage options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="AzureBlobLuggageOptions"/> containing the necessary
    /// connection details for the Azure Blob container.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown if the provided <paramref name="options"/> do not contain a valid
    /// combination of connection details (either ContainerUri and Credential,
    /// or ConnectionString and ContainerName must be provided).
    /// </exception>
    public AzureBlobLuggageStore(AzureBlobLuggageOptions options)
    {
        _options = options;
        if (options.ContainerUri != null && options.Credential != null)
        {
            _blobClient = new BlobContainerClient(options.ContainerUri, options.Credential);
        }
        else if (string.IsNullOrEmpty(options.ContainerName) && string.IsNullOrEmpty(options.ConnectionString))
        {
            _blobClient = new BlobContainerClient(options.ConnectionString, options.ContainerName);
        }
        else
        {
            throw new ArgumentException("", nameof(options));
        }
    }
    
        
    /// <inheritdoc cref="IAmAStorageProvider.Tracer"/>
    public IAmABrighterTracer? Tracer { get; set; }

    /// <inheritdoc />
    public async Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Strategy == StorageStrategy.Assume)
        {
            return;
        }

        if (await _blobClient.ExistsAsync(cancellationToken))
        {
            return;
        }

        if (_options.Strategy == StorageStrategy.Validate)
        {
            throw new InvalidOperationException($"There was no luggage store with the container name {_blobClient.Name}");
        }

        await _blobClient.CreateAsync(
            metadata: _options.Metadata,
            encryptionScopeOptions: _options.EncryptionScopeOptions,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Delete the luggage identified by the claim check
    /// Used to clean up after luggage is retrieved
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    /// <param name="cancellationToken">The cancellation token</param>
    public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Delete,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            return blobClient.DeleteAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Downloads the luggage associated with the claim check
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>The luggage as a stream</returns>
    public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.HasClaim,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);

            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Do we have luggage for this claim check - in case of error or deletion
    /// </summary>
    /// <param name="claimCheck"></param>
    /// <param name="cancellationToken">The cancellation token</param>
    public Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            return blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <summary>
    /// Puts luggage into the store and provides a claim check for that luggage
    /// </summary>
    /// <param name="stream">A stream representing the luggage to check</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A claim check for the luggage stored</returns>
    public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken)
    {
        var claimCheck = Uuid.NewAsString();
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Store,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes,
            stream.Length
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            await blobClient.UploadAsync(stream, cancellationToken);

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

        if (_blobClient.Exists())
        {
            return;
        }

        if (_options.Strategy == StorageStrategy.Validate)
        {
            throw new InvalidOperationException($"There was no luggage store with the container name {_blobClient.Name}");
        }

        _blobClient.Create(
            metadata: _options.Metadata,
            encryptionScopeOptions: _options.EncryptionScopeOptions);
    }

    /// <summary>
    /// Delete the luggage identified by the claim check
    /// Used to clean up after luggage is retrieved
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    public void Delete(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Delete,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            blobClient.Delete();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
        
    /// <summary>
    /// Downloads the luggage associated with the claim check
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    /// <returns>The luggage as a stream</returns>
    public Stream Retrieve(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            return blobClient.OpenRead();
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
        
    /// <summary>
    /// Downloads the luggage associated with the claim check
    /// </summary>
    /// <param name="claimCheck">The claim check for the luggage</param>
    /// <returns>The luggage as a stream</returns>
    public bool HasClaim(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.HasClaim,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);

            var response = blobClient.Exists();
            return response.Value;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
        
    /// <summary>
    /// Puts luggage into the store and provides a claim check for that luggage
    /// </summary>
    /// <param name="stream">A stream representing the luggage to check</param>
    /// <returns>A claim check for the luggage stored</returns>
    public string Store(Stream stream)
    {
        var claimCheck = Uuid.NewAsString();
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Store,
            ClaimCheckProvider,
            _blobClient.Name,
            claimCheck,
            _spanAttributes,
            stream.Length
        ));

        try
        {
            var blobClient = _blobClient.GetBlobClient(claimCheck);
            blobClient.Upload(stream);

            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}
