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
public class FirestoreDistributedLock(FirestoreConfiguration configuration) : IDistributedLock
{
    /// <inheritdoc />
    public async Task<string?> ObtainLockAsync(string resource, CancellationToken cancellationToken)
    {
        try
        {
            var client = await configuration.CreateFirestoreClientAsync(cancellationToken);
            await client.CommitAsync(new CommitRequest
            {
                Database = configuration.Database,
                Writes =
                {
                    new Write
                    {
                        Update = new Document
                        {
                            Name = resource,
                            Fields =
                            {
                                ["Resource"] = new Value { StringValue = resource },
                                ["CreatedAt"] = new Value { TimestampValue = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow) },
                            }
                        },
                        CurrentDocument = new Precondition{ Exists = false }
                    }
                }
            }, CallSettings.FromCancellationToken(cancellationToken));

            return resource;
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
            var client = await configuration.CreateFirestoreClientAsync(cancellationToken);
            await client.DeleteDocumentAsync(
                new DeleteDocumentRequest { Name = configuration.GetDocumentName(lockId) },
                CallSettings.FromCancellationToken(cancellationToken));
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            // Ignore when the document not exists
        }
    }
}
