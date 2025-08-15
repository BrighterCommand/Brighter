using System;

namespace Paramore.Brighter.Firestore;

public class FirestoreCollection
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan? Ttl { get; set; }
}
