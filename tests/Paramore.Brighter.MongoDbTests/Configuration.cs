using Paramore.Brighter.MongoDb;

namespace Paramore.Brighter.MongoDbTests;

public class Configuration
{
    public static MongoDbConfiguration Create(string collection)
    {
        return new MongoDbConfiguration("mongodb://root:example@localhost:27017", "brighter", collection);
    }

    public static void Cleanup(string collection)
    {
        var config = Create(collection);
        config.Client.GetDatabase(config.DatabaseName).DropCollection(collection);
    }
}
