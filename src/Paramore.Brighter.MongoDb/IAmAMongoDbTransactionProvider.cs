using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents a contract for providing MongoDB connection capabilities and
/// managing transactions specifically for outbox/inbox patterns within Brighter.
/// This interface combines MongoDB client access with transaction management
/// using <see cref="IClientSession"/> for ACID guarantees.
/// </summary>
public interface IAmAMongoDbTransactionProvider : IAmAMongoDbConnectionProvider, IAmABoxTransactionProvider<IClientSession>;
