using System;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Storage.v1.Data;
using Google.Cloud.Storage.V1;
using Grpc.Core;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.Gcp;

/// <summary>
/// Configuration options for the Google Cloud Storage (GCS) luggage store provider in the Brighter framework.
/// Provides settings for connecting to GCS buckets and configuring storage behavior [[1]].
/// </summary>
/// <remarks>
/// <para>
/// This class extends <see cref="StorageOptions"/> to provide GCP-specific configuration for storing 
/// message payloads ("luggage") in Google Cloud Storage. Key settings include project/bucket identifiers,
/// authentication credentials, and client configuration hooks.
/// </para>
/// <para>
/// When using <see cref="StorageStrategy.CreateIfMissing"/>, ensure the configured credentials have 
/// permissions to create buckets in the target GCP project [[8]].
/// </para>
/// </remarks>
public class GcsLuggageOptions : StorageOptions
{
    /// <summary>
    /// Gets or sets the Google Cloud Platform (GCP) project ID.
    /// This is required for all operations except when using <see cref="StorageStrategy.Assume"/>.
    /// </summary>
    /// <example>"my-gcp-project"</example>
    /// <exception cref="ConfigurationException">Thrown if empty during storage operations</exception>
    public string ProjectId { get; set; } = string.Empty;
    

    /// <summary>
    /// Gets or sets the name of the Google Cloud Storage bucket to use.
    /// This is required for all operations except when using <see cref="StorageStrategy.Assume"/>.
    /// </summary>
    /// <example>"brighter-luggage-store"</example>
    /// <exception cref="ConfigurationException">Thrown if empty during storage operations</exception>
    public string BucketName
    {
        get => Bucket.Name; 
        set => Bucket.Name = value;
    } 
    
    /// <summary>
    /// Gets or sets an explicit <see cref="Bucket"/> object to use for storage operations instead of resolving by name.
    /// This allows pre-configured bucket objects with custom access controls or metadata to be used directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When set, this property takes precedence over <see cref="BucketName"/> and <see cref="ProjectId"/> for most operations.
    /// The bucket must already exist and be properly configured for Brighter's usage patterns.
    /// </para>
    /// <para>
    /// Useful when you need to enforce specific bucket policies or lifecycle configurations that differ from the default behavior [[6]].
    /// If null, the system will resolve the bucket by name using the configured <see cref="GetBucketOptions"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// // Using a pre-configured bucket with custom settings
    /// var bucket = new Bucket
    /// {
    ///     Name = "custom-luggage-bucket",
    ///     Acl = new List<BucketAccessControl> 
    ///     { 
    ///         new() { Entity = "project-owners-12345", Role = "OWNER" } 
    ///     }
    /// };
    /// 
    /// options.Bucket = bucket;
    /// </example>
    public Bucket Bucket { get; set; } = new();
    
    /// <summary>
    /// Gets or sets an optional prefix to apply to all stored objects in the bucket.
    /// Useful for organizing luggage items within a bucket.
    /// </summary>
    /// <example>"production/" or "environment/dev/"</example>
    public string Prefix { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the OAuth2 credentials to use for authenticating with Google Cloud Storage.
    /// If null, the client will use default application credentials.
    /// </summary>
    /// <seealso cref="GoogleCredential.GetApplicationDefault"/>
    public ICredential? Credential { get; set; }
   
    /// <summary>
    /// Gets or sets options for creating a new bucket when using <see cref="StorageStrategy.CreateIfMissing"/>.
    /// Only used if the bucket does not already exist.
    /// </summary>
    public CreateBucketOptions? CreateBucketOptions { get; set; }
    
    /// <summary>
    /// Gets or sets options for retrieving bucket metadata when calling <see cref="StorageClient.GetBucket"/> or <see cref="StorageClient.GetBucketAsync"/>.
    /// Useful for customizing request behavior like specifying projection or user project parameters [[4]].
    /// </summary>
    /// <remarks>
    /// Only used during bucket validation operations when <see cref="StorageStrategy.Validate"/> is configured.
    /// </remarks>
    public GetBucketOptions? GetBucketOptions { get; set; }
    
    /// <summary>
    /// Gets or sets options for retrieving objects when calling <see cref="StorageClient.GetObject"/> or <see cref="StorageClient.GetObjectAsync"/>.
    /// Allows customization of object retrieval behavior including generation constraints and projection settings.
    /// </summary>
    /// <remarks>
    /// Applied during <see cref="GcsLuggageStore.Retrieve"/> and <see cref="GcsLuggageStore.HasClaim"/> operations to control metadata scope and versioning.
    /// </remarks>
    public GetObjectOptions? GetObjectOptions { get; set; }
    
    /// <summary>
    /// Gets or sets options for deleting objects when calling <see cref="StorageClient.DeleteObject(string,string,Google.Cloud.Storage.V1.DeleteObjectOptions)"/> or <see cref="StorageClient.DeleteObjectAsync(string,string,Google.Cloud.Storage.V1.DeleteObjectOptions,System.Threading.CancellationToken)"/>.
    /// Enables advanced configuration of deletion behavior including generation constraints and user project parameters.
    /// </summary>
    /// <remarks>
    /// Used in all <see cref="GcsLuggageStore.Delete"/> operations to control conditional deletion and billing settings.
    /// </remarks>
    public DeleteObjectOptions? DeleteObjectOptions { get; set; }
    
    /// <summary>
    /// Gets or sets options for downloading object content when calling <see cref="StorageClient.DownloadObject(string,string,System.IO.Stream,Google.Cloud.Storage.V1.DownloadObjectOptions,System.IProgress{Google.Apis.Download.IDownloadProgress})"/> or <see cref="StorageClient.DownloadObjectAsync(string,string,System.IO.Stream,Google.Cloud.Storage.V1.DownloadObjectOptions,System.Threading.CancellationToken,System.IProgress{Google.Apis.Download.IDownloadProgress})"/>.
    /// Controls download behavior including buffer size and cancellation tokens.
    /// </summary>
    /// <remarks>
    /// Affects stream buffering and progress tracking during payload retrieval operations.
    /// </remarks>
    public DownloadObjectOptions? DownloadObjectOptions { get; set; }
    
    /// <summary>
    /// Gets or sets options for uploading objects when calling <see cref="StorageClient.UploadObject(string,string,string,System.IO.Stream,Google.Cloud.Storage.V1.UploadObjectOptions,System.IProgress{Google.Apis.Upload.IUploadProgress})"/> or <see cref="StorageClient.UploadObjectAsync(string,string,string,System.IO.Stream,Google.Cloud.Storage.V1.UploadObjectOptions,System.Threading.CancellationToken,System.IProgress{Google.Apis.Upload.IUploadProgress})"/>.
    /// Allows configuration of upload behavior including content encoding and predefined ACL settings.
    /// </summary>
    /// <remarks>
    /// Used in all <see cref="GcsLuggageStore"/> operations to control upload resumability and metadata settings.
    /// </remarks> 
    public UploadObjectOptions? UploadObjectOptions { get; set; }
    
    /// <summary>
    /// Gets or sets a callback to customize the <see cref="StorageClientBuilder"/> before client creation.
    /// Allows advanced configuration of retry policies, scopes, or endpoint settings.
    /// </summary>
    /// <example>
    /// options.ClientBuilderConfigurator = builder => 
    ///     builder.Scopes = new[] { "https://www.googleapis.com/auth/cloud-platform " };
    /// </example>
    public Action<StorageClientBuilder>? ClientBuilderConfigurator { get; set; }

    /// <summary>
    /// Creates a synchronous <see cref="StorageClient"/> instance using the configured options.
    /// </summary>
    /// <returns>A configured <see cref="StorageClient"/> instance</returns>
    /// <exception cref="RpcException">Propagates Google Cloud API errors</exception>
    /// <exception cref="InvalidOperationException">Thrown if client creation fails</exception>
    public StorageClient CreateStorageClient()
    {
        var builder = new StorageClientBuilder{Credential = Credential};
        ClientBuilderConfigurator?.Invoke(builder);
        return builder.Build();
    }
    
    /// <summary>
    /// Asynchronously creates a <see cref="StorageClient"/> instance using the configured options.
    /// </summary>
    /// <returns>A task that resolves to a configured <see cref="StorageClient"/> instance</returns>
    /// <exception cref="RpcException">Propagates Google Cloud API errors</exception>
    /// <exception cref="InvalidOperationException">Thrown if client creation fails</exception>
    public Task<StorageClient> CreateStorageClientAsync()
    {
        var builder = new StorageClientBuilder{Credential = Credential};
        ClientBuilderConfigurator?.Invoke(builder);
        return builder.BuildAsync();
    }
}
