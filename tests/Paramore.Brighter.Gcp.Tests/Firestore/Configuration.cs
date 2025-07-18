using Paramore.Brighter.Firestore;
using Paramore.Brighter.Gcp.Tests.Helper;

namespace Paramore.Brighter.Gcp.Tests.Firestore;

public static class Configuration
{
    public static FirestoreConfiguration CreateInbox(string collection)
    {
        return new FirestoreConfiguration (GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Inbox = collection
        };
    }
    
    public static FirestoreConfiguration CreateOutbox(string collection)
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Outbox =  collection
        };
    }
    
    public static FirestoreConfiguration CreateLocking(string collection)
    {
        return new FirestoreConfiguration(GatewayFactory.GetProjectId(), DatabaseName)
        {
            Credential = GatewayFactory.GetCredential(),
            Locking = collection
        };
    }

    public static void Cleanup(string collection)
    {
        // var config = new MongoDbConfiguration(ConnectionString, DatabaseName);
        // config.Client.GetDatabase(config.DatabaseName).DropCollection(collection);
    }

    public const string DatabaseName = "brighter";
}
