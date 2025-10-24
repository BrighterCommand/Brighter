using System;
using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Firestore;

public static class Configuration
{
    public static FirestoreConfiguration CreateInbox()
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Inbox = new FirestoreCollection
            {
                Name = InboxCollection,
                Ttl = TimeSpan.FromMinutes(5)
            }
        };
    }
    
    public static FirestoreConfiguration CreateOutbox()
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Outbox = new FirestoreCollection
            {
                Name = OutboxCollection,
                Ttl = TimeSpan.FromMinutes(5)
            } 
        };
    }
    
    public static FirestoreConfiguration CreateLocking(string collection)
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Locking = new FirestoreCollection
            {
                Name = LockingCollection,
                Ttl = TimeSpan.FromMinutes(5)
            } 
        };
    }

    private const string DatabaseName = "brighter-firestore-database";
    private const string InboxCollection = "inbox";
    private const string OutboxCollection = "outbox";
    private const string LockingCollection = "locking";
}
