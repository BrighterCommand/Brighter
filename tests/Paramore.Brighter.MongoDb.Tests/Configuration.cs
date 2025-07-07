using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.MongoDb.Tests;

public class Configuration
{
    public static MongoDbConfiguration Create(string collection)
    {
        return new MongoDbConfiguration(ConnectionString, DatabaseName, collection);
    }

    public static void Cleanup(string collection)
    {
        var config = Create(collection);
        config.Client.GetDatabase(config.DatabaseName).DropCollection(collection);
    }

    public const string ConnectionString = "mongodb://root:example@localhost:27017";
    public const string DatabaseName = "brighter";
}
