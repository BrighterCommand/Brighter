using System.Collections.Concurrent;
using System.Collections.Generic;
using Google.Cloud.Firestore.V1;
using Paramore.Brighter.Extensions;

namespace Paramore.Brighter.Outbox.Firestore;

public class FirestoreTransaction
{
    private readonly ConcurrentBag<Write> _writes = new();

    public void Add(Write write) => _writes.Add(write);
    public void AddRange(IEnumerable<Write> writes) => writes.Each(x => _writes.Add(x));
    
    public IEnumerable<Write> Writes => _writes;
}
