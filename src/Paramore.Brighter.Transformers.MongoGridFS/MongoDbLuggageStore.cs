using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.MongoGridFS;

/// <summary>
/// Implements a storage provider for the Brighter Claim Check pattern using MongoDB GridFS.
/// This class enables storing, retrieving, deleting, and checking the existence of
/// large message payloads (luggage) within a MongoDB database using the GridFS specification.
/// </summary>
/// <remarks>
/// GridFS is a specification for storing and retrieving files that exceed the BSON-document
/// size limit of 16 MB. Instead of storing a file in a single document, GridFS divides
/// the file into chunks and stores each chunk as a separate document.
/// <para>
/// This provider leverages the MongoDB .NET Driver's <see cref="GridFSBucket"/> to
/// interact with GridFS, providing both synchronous and asynchronous operations
/// conforming to Brighter's <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/> interfaces.
/// </para>
/// <para>
/// **Bucket Creation:**
/// The underlying GridFS collections (e.g., `fs.files` and `fs.chunks` for the default bucket)
/// are implicitly created by the MongoDB driver on the first write operation if they do not already exist.
/// </para>
/// **Configuration:**
/// Instances of this class are configured via <see cref="MongoDbLuggageStoreOptions"/>,
/// which specifies the MongoDB connection string, database name, and optional GridFS bucket settings.
/// </remarks>
public class MongoDbLuggageStore : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "mongodb_gridfs";
    private readonly Dictionary<string, string> _spanAttributes = new();
    private readonly GridFSBucket _bucket;
    private readonly MongoDbLuggageStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoDbLuggageStore"/> class
    /// with the specified MongoDB GridFS storage options.
    /// </summary>
    /// <param name="options">
    /// The <see cref="MongoDbLuggageStoreOptions"/> containing the MongoDB connection string,
    /// database name, and optional GridFS bucket configuration.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the <see cref="MongoDbLuggageStoreOptions.ConnectionString"/>
    /// or <see cref="MongoDbLuggageStoreOptions.DatabaseName"/> properties are null or empty.
    /// </exception>
    public MongoDbLuggageStore(MongoDbLuggageStoreOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        var client = new MongoClient(options.ConnectionString);
        _bucket = new GridFSBucket(client.GetDatabase(options.DatabaseName), options.BucketOptions);
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

        var database = _bucket.Database;
        var fileCollection = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", $"{_options.BucketOptions.BucketName}.files") }, cancellationToken);
        var chunksCollection = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = new BsonDocument("name", $"{_options.BucketOptions.BucketName}.chunks") }, cancellationToken);

        if (await fileCollection.AnyAsync(cancellationToken: cancellationToken) && await chunksCollection.AnyAsync(cancellationToken: cancellationToken))
        {
            return;
        }

        if (_options.Strategy == StorageStrategy.Validate)
        {
            throw new InvalidOperationException("bucket not exists");
        }
        
        // If the collection not exists, when we upload data to there 
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Delete,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, claimCheck);
            using var cursor = await _bucket.FindAsync(filter, cancellationToken: cancellationToken);
            if (!await cursor.MoveNextAsync(cancellationToken))
            {
                return;
            }
            
            var file = cursor.Current.First();
            await _bucket.DeleteAsync(file.Id, cancellationToken);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
         var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var memory = new MemoryStream();
            await _bucket.DownloadToStreamByNameAsync(claimCheck, memory, _options.DownloadOptions, cancellationToken);
            memory.Position = 0;
            return memory; 
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
       var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));
        
        try
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, claimCheck);
            using var cursor = await _bucket.FindAsync(filter, cancellationToken: cancellationToken);
            return await cursor.MoveNextAsync(cancellationToken);
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
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Store,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes,
            stream.Length
        ));
        
        try
        {
            await _bucket.UploadFromStreamAsync(claimCheck, stream, _options.UploadOptions, cancellationToken);
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

        var database = _bucket.Database;
        var fileCollection = database.ListCollections(new ListCollectionsOptions { Filter = new BsonDocument("name", $"{_options.BucketOptions.BucketName}.files") });
        var chunksCollection = database.ListCollections(new ListCollectionsOptions { Filter = new BsonDocument("name", $"{_options.BucketOptions.BucketName}.chunks") });

        if (fileCollection.Any() && chunksCollection.Any())
        {
            return;
        }

        if (_options.Strategy == StorageStrategy.Validate)
        {
            throw new InvalidOperationException("bucket not exists");
        }
        
        // If the collection not exists, when we upload data to there 
    }

    /// <inheritdoc />
    public void Delete(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Delete,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, claimCheck);
            using var cursor = _bucket.Find(filter);
            if (!cursor.MoveNext())
            {
                return;
            }
            
            var file = cursor.Current.First();
            _bucket.Delete(file.Id);
        }
        catch (GridFSFileNotFoundException)
        {
            // Ignore case the file not exists
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Stream Retrieve(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));

        try
        {
            var memory = new MemoryStream();
            _bucket.DownloadToStreamByName(claimCheck, memory, _options.DownloadOptions);
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
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Retrieve,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes
        ));
        
        try
        {
            var filter = Builders<GridFSFileInfo>.Filter.Eq(x => x.Filename, claimCheck);
            using var cursor = _bucket.Find(filter);
            return cursor.MoveNext();
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
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(
            ClaimCheckOperation.Store,
            ClaimCheckProvider,
            _options.BucketOptions.BucketName,
            claimCheck,
            _spanAttributes,
            stream.Length
        ));
        
        try
        {
            _bucket.UploadFromStream(claimCheck, stream, _options.UploadOptions);
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }
}
