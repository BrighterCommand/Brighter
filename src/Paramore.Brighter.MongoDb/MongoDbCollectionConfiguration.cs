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
    /// If set, a TTL index on the timestamp's stored BSON date automatically removes documents
    /// after this duration. A null value indicates no TTL is configured at this level.
    /// </summary>
    /// <remarks>
    /// When set, the TTL index is ensured on first collection access in every resolution mode,
    /// including for existing collections. The database user must have permission to create indexes.
    /// An identical index is reused. Conflicting index options or insufficient permissions cause
    /// collection access to fail; Brighter does not replace or modify an existing index.
    /// With the default DateTimeOffset document representation, the index targets <c>TimeStamp.DateTime</c>.
    /// A BSON date representation keeps the index on <c>TimeStamp</c>. Mapped field names are respected.
    /// For collections using the document representation with a <c>brighter_ttl_{collectionName}</c>
    /// index on <c>TimeStamp</c>, stop old application instances before dropping the index and
    /// recreating it on <c>TimeStamp.DateTime</c> with the same name and expiration duration.
    /// Then start the upgraded application. Existing documents need no conversion, but documents
    /// already older than the TTL become eligible for deletion as soon as the corrected index exists.
    /// With <see cref="OnResolvingACollection.Assume"/>, creating the index can implicitly create
    /// a missing collection. <see cref="OnResolvingACollection.Validate"/> still requires the collection to exist.
    /// </remarks>
    public TimeSpan? TimeToLive { get; set; }
}
