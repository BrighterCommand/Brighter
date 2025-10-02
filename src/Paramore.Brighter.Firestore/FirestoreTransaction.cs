using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Cloud.Firestore.V1;
using Google.Protobuf;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Firestore;

/// <summary>
/// Represents a helper class for managing write operations within a Firestore transaction.
/// This class collects individual <see cref="Write"/> operations and associates them
/// with a specific transaction ID, allowing them to be committed atomically.
/// </summary>
public class FirestoreTransaction(ByteString transaction)
{
    private readonly ConcurrentBag<Write> _writes = new();

    /// <summary>
    /// Adds a single write operation to the collection of writes for this transaction.
    /// </summary>
    /// <param name="write">The <see cref="Write"/> operation to add.</param>
    public void Add(Write write) => _writes.Add(write);
    
    /// <summary>
    /// Adds a range of write operations to the collection of writes for this transaction.
    /// </summary>
    /// <param name="writes">An enumerable collection of <see cref="Write"/> operations to add.</param>
    public void AddRange(IEnumerable<Write> writes) => writes.Each(x => _writes.Add(x));
    
    /// <summary>
    /// Gets a read-only collection of all write operations accumulated for this transaction.
    /// </summary>
    public IEnumerable<Write> Writes => _writes;

    /// <summary>
    /// Gets the unique identifier for the Firestore transaction.
    /// </summary>
    public ByteString Transaction { get; } = transaction;
}
