using MongoDB.Driver;

namespace Paramore.Brighter.MongoDb;

/// <summary>
/// Represents the configuration settings for a specific MongoDB collection used within Brighter.
/// This class provides options for defining the collection's name, how it's resolved,
/// its specific settings, creation options, and optional Time-To-Live (TTL) duration.
/// </summary>
public class MongoDbCollectionConfiguration
{
    /// <summary>
    /// Gets or sets the name of the MongoDB collection.
    /// This is a mandatory property for identifying the collection.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the action to be performed when resolving a collection.
    /// This property determines the behavior when attempting to access or create the collection,
    /// such as assuming it exists, explicitly creating it, or validating its existence.
    /// </summary>
    public OnResolvingACollection MakeCollection { get; set; } = OnResolvingACollection.Assume;
    
    /// <summary>
    /// Gets or sets the <see cref="MongoCollectionSettings"/> used when retrieving or accessing the collection.
    /// These settings can include options like read preference, write concern, and other collection-specific behaviors.
    /// </summary>
    public MongoCollectionSettings? Settings { get; set; }
    
    /// <summary>
    /// Gets or sets the <see cref="CreateCollectionOptions"/> to be used if the collection needs to be explicitly created.
    /// These options allow for configuring properties such as capping, validation rules, or time-series settings during collection creation.
    /// </summary>
    public CreateCollectionOptions? CreateCollectionOptions { get; set; }
    
    /// <summary>
    /// Gets or sets the Time-To-Live (TTL) duration for documents within this collection.
    /// If set, a TTL index will be configured on a specified field (e.g., `TimeStamp`)
    /// to automatically remove documents after this duration. A null value indicates no TTL is configured at this level.
    /// </summary>
    public TimeSpan? TimeToLive { get; set; }
}
