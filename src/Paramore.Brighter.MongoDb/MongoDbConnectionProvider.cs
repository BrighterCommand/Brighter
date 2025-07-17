using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Provides a concrete implementation of <see cref="IAmAMongoDbConnectionProvider"/>,
/// serving as a simple provider for a MongoDB client instance based on the
/// provided configuration.
/// </summary>
/// <param name="configuration">
/// The MongoDB configuration, which must include a non-null <see cref="IMongoClient"/> instance.
/// </param>
public class MongoDbConnectionProvider(IAmAMongoDbConfiguration configuration) : IAmAMongoDbConnectionProvider
{
    /// <inheritdoc />
    public IMongoClient Client { get; } = configuration.Client ?? throw new ArgumentNullException(nameof(configuration));
}
