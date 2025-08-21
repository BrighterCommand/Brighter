using Google.Api.Gax.Grpc;
using Google.Cloud.Firestore.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Paramore.Brighter.Firestore;
using Value = Google.Cloud.Firestore.V1.Value;

namespace Paramore.Brighter.Locking.Firestore;

/// <summary>
/// Implements a distributed lock using Google Cloud Firestore.
/// This implementation relies on Firestore's atomic write operations and
/// the "AlreadyExists" status code to ensure only one client can create
/// a lock document for a given resource at a time.
/// </summary>
public class FirestoreDistributedLock : IDistributedLock
{
    private readonly IAmAFirestoreConnectionProvider _connectionProvider;
    private readonly FirestoreConfiguration _configuration;
    private readonly FirestoreCollection _collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirestoreDistributedLock"/> class with just
    /// the Firestore configuration. This constructor internally creates a default
    /// <see cref="FirestoreConnectionProvider"/> based on the provided configuration.
    /// </summary>
    /// <param name="configuration">The configuration settings for connecting to Firestore,
    /// including project ID, database ID, and collection names for locks.</param>
    public FirestoreDistributedLock(FirestoreConfiguration configuration)
        : this(new FirestoreConnectionProvider(configuration), configuration)
    {
        
    }

    /// <summary>
    /// Implements a distributed lock using Google Cloud Firestore.
    /// This implementation relies on Firestore's atomic write operations and
    /// the "AlreadyExists" status code to ensure only one client can create
    /// a lock document for a given resource at a time.
    /// </summary>
    public FirestoreDistributedLock(IAmAFirestoreConnectionProvider connectionProvider, FirestoreConfiguration configuration)
    {
        _connectionProvider = connectionProvider;
        _configuration = configuration;
        if (configuration.Locking == null || string.IsNullOrEmpty(configuration.Locking.Name))
        {
            throw new ArgumentException("locking collection can't be null or empty", nameof(configuration));
        }
        
        _collection = configuration.Locking;
    }

    /// <inheritdoc />
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        try
        {
            Value ttl;
            if (_collection.Ttl.HasValue && _collection.Ttl != TimeSpan.Zero)
            {
                ttl = new Value { TimestampValue = Timestamp.FromDateTimeOffset(_configuration.TimeProvider.GetUtcNow() + _collection.Ttl.Value) };
            }
            else
            {
                ttl = new Value { NullValue = NullValue.NullValue };
            }
            
            var lockId = resource
                .Replace("/", "-")
                .Replace(".", "-");
            
            var client = await _connectionProvider.GetFirestoreClientAsync(cancellationToken);
            await client.CommitAsync(new CommitRequest
            {
                Database = _configuration.Database,
                Writes =
                {
                    new Write
                    {
                        Update = new Document
                        {
                            Name = _configuration.GetDocumentName(_collection.Name, lockId),
                            Fields =
                            {
                                ["Resource"] = new Value { StringValue = resource },
                                ["CreatedAt"] = new Value { TimestampValue = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow) },
                                ["Ttl"] = ttl
                            }
                        },
                        CurrentDocument = new Precondition{ Exists = false }
                    }
                }
            }, CallSettings.FromCancellationToken(cancellationToken));

            return lockId;
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.AlreadyExists)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ReleaseLockAsync(string resource, string lockId, CancellationToken cancellationToken)
    {
        try
        {
            var client = await _connectionProvider.GetFirestoreClientAsync(cancellationToken);
            await client.DeleteDocumentAsync(
                new DeleteDocumentRequest { Name = _configuration.GetDocumentName(_collection.Name, lockId) },
                CallSettings.FromCancellationToken(cancellationToken));
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            // Ignore when the document not exists
        }
    }
}
