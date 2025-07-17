using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents a contract for providing access to a MongoDB client.
/// This interface extends <see cref="IAmAConnectionProvider"/>, specifically
/// tailoring it for MongoDB connections within the Brighter framework.
/// </summary>
public interface IAmAMongoDbConnectionProvider  : IAmAConnectionProvider
{
    /// <summary>
    /// Gets the <see cref="IMongoClient"/> instance managed by this provider.
    /// This client is the primary interface for interacting with the MongoDB database.
    /// </summary>
    IMongoClient Client { get; }
}
