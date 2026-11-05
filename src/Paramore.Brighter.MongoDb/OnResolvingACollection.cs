namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Defines the possible actions to take when a MongoDB collection is being resolved
/// within the application. This enum provides options for how to handle the existence
/// or non-existence of a target collection.
/// </summary>
public enum OnResolvingACollection
{
    /// <summary>
    /// Instructs the system to assume the collection already exists.
    /// If the collection does not actually exist, MongoDB will implicitly create it
    /// upon the first write operation (e.g., insert, update, upsert). No explicit
    /// creation or validation check is performed.
    /// </summary>
    /// <remarks>
    /// A configured <see cref="MongoDbCollectionConfiguration.TimeToLive"/> still ensures a TTL index
    /// on first access. MongoDB can implicitly create a missing collection during index creation.
    /// </remarks>
    Assume,
    
    /// <summary>
    /// Instructs the system to validate that the collection exists.
    /// If the specified collection does not exist in the database, an exception
    /// will be thrown, indicating a configuration error or missing database setup.
    /// </summary>
    /// <remarks>
    /// Once the collection is validated, a configured <see cref="MongoDbCollectionConfiguration.TimeToLive"/>
    /// ensures the TTL index exists.
    /// </remarks>
    Validate,
    
    /// <summary>
    /// Instructs the system to create the collection if it does not already exist.
    /// If the collection already exists, it is reused without changing its collection options.
    /// This option is useful for ensuring that collections with specific options (e.g.,
    /// capped collections, validation rules, or TTL indexes) are properly initialized.
    /// </summary>
    /// <remarks>
    /// A configured <see cref="MongoDbCollectionConfiguration.TimeToLive"/> ensures the TTL index
    /// for both new and existing collections. Index creation errors propagate to the caller.
    /// </remarks>
    CreateIfNotExists
}
