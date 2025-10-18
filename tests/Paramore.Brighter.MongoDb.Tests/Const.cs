using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb.Tests;

public static class Const
{
    public const string ConnectionString = "mongodb://root:example@localhost:27017";
    public const string DatabaseName = "brighter";
    
    public static IMongoClient Client { get; } = new MongoClient(ConnectionString);
}
