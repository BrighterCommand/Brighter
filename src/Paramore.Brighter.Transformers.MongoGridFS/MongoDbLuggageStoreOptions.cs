using MongoDB.Driver.GridFS;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.MongoGridFS;

/// <summary>
/// Provides configuration options for the <see cref="MongoDbLuggageStore"/>.
/// These options define how to connect to a MongoDB database and specify the GridFS bucket
/// to be used for storing and retrieving claim check payloads.
/// </summary>
/// <remarks>
/// This class extends <see cref="StorageOptions"/> to include settings specific
/// to MongoDB GridFS storage. It uses a primary constructor to enforce the provision
/// of essential connection details: a connection string, database name, and GridFS bucket name.
/// <para>
/// **Important Considerations:**
/// <list type="bullet">
///     <item>
///         <term>Connection String:</term>
///         <description>Ensure the <see cref="ConnectionString"/> provides valid access to your MongoDB instance.</description>
///     </item>
///     <item>
///         <term>Database Name:</term>
///         <description>The <see cref="DatabaseName"/> specifies which database within MongoDB will host the GridFS bucket.</description>
///     </item>
///     <item>
///         <term>Bucket Name:</term>
///         <description>The <see cref="GridFSBucketOptions.BucketName"/> (derived from the constructor's <paramref name="bucketName"/>) determines the prefix for the GridFS collections (e.g., `mybucket.files` and `mybucket.chunks`).</description>
///     </item>
/// </list>
/// </para>
/// </remarks>
public class MongoDbLuggageStoreOptions(string connectionString, string database, string bucketName) : StorageOptions
{ 
    /// <summary>
    /// Gets or sets the MongoDB connection string.
    /// This string specifies the server address, port, authentication details, etc.,
    /// required to connect to your MongoDB instance.
    /// </summary>
    /// <value>A valid MongoDB connection string. This property is mandatory.</value>
    /// <example>
    /// <c>"mongodb://localhost:27017"</c> or <c>"mongodb+srv://user:password@cluster.mongodb.net/test"</c>
    /// </example>
    public string ConnectionString { get; set; } = connectionString;
    
    /// <summary>
    /// Gets or sets the name of the MongoDB database where the GridFS bucket will reside.
    /// </summary>
    /// <value>The name of the database. This property is mandatory.</value>
    public string DatabaseName { get; set; } = database;

    /// <summary>
    /// Gets or sets the options for the GridFS bucket itself, including its name.
    /// The <see cref="GridFSBucketOptions.BucketName"/> is initialized from the
    /// constructor's <paramref name="bucketName"/> parameter.
    /// </summary>
    /// <value>An instance of <see cref="GridFSBucketOptions"/>.</value> 
    public GridFSBucketOptions BucketOptions { get; set; } = new()
    {
        BucketName = bucketName
    };

    /// <summary>
    /// Gets or sets optional specific download options for GridFS operations.
    /// This can include settings like the chunk size or custom metadata handling during downloads.
    /// </summary>
    /// <value>An optional instance of <see cref="GridFSDownloadByNameOptions"/>.</value>
    public GridFSDownloadByNameOptions? DownloadOptions { get; set; }
    
    /// <summary>
    /// Gets or sets optional specific upload options for GridFS operations.
    /// This can include settings like the chunk size, metadata to attach, or write concern during uploads.
    /// </summary>
    /// <value>An optional instance of <see cref="GridFSUploadOptions"/>.</value>
    public GridFSUploadOptions? UploadOptions { get; set; }
}
