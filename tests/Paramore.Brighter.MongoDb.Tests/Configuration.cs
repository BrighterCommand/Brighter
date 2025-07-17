using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests;

public class Configuration
{
    public static MongoDbConfiguration CreateInbox(string collection)
    {
        return new MongoDbConfiguration(ConnectionString, DatabaseName)
        {
            Inbox = new MongoDbCollectionConfiguration
            {
                Name = collection
            }
        };
    }
    
    public static MongoDbConfiguration CreateOutbox(string collection)
    {
        return new MongoDbConfiguration(ConnectionString, DatabaseName)
        {
            Outbox = new MongoDbCollectionConfiguration
            {
                Name = collection
            }
        };
    }
    
    public static MongoDbConfiguration CreateLocking(string collection)
    {
        return new MongoDbConfiguration(ConnectionString, DatabaseName)
        {
            Locking = new MongoDbCollectionConfiguration
            {
                Name = collection
            }
        };
    }

    public static void Cleanup(string collection)
    {
        var config = new MongoDbConfiguration(ConnectionString, DatabaseName);
        config.Client.GetDatabase(config.DatabaseName).DropCollection(collection);
    }

    public const string ConnectionString = "mongodb://root:example@localhost:27017";
    public const string DatabaseName = "brighter";
}
