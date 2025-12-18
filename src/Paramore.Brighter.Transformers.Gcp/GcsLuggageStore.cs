using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Google;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.Gcp;

/// <summary>
/// A Google Cloud Storage (GCS) implementation of the <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/> interfaces for the Brighter framework.
/// Provides synchronous and asynchronous storage operations for message "luggage" (payloads) using GCP's Cloud Storage buckets.
/// </summary>
/// <remarks>
/// <para>
/// This class enables Brighter to store, retrieve, and manage message payloads in Google Cloud Storage. 
/// It supports bucket creation/validation via <see cref="StorageStrategy"/> and integrates with Brighter's tracing and logging systems.
/// </para>
/// <para>
/// <strong>Configuration:</strong>
/// Requires valid <see cref="GcsLuggageOptions"/> containing:
/// - <see cref="GcsLuggageOptions.ProjectId"/>: GCP project ID
/// - <see cref="GcsLuggageOptions.BucketName"/>: Target bucket name
/// - <see cref="GcsLuggageOptions.Strategy"/>: Bucket management strategy (Assume/Validate/Create)
/// </para>
/// <para>
/// <strong>Error Handling:</strong>
/// Throws <see cref="ConfigurationException"/> for missing project/bucket settings, 
/// <see cref="InvalidOperationException"/> for validation failures, 
/// and propagates GCS-specific <see cref="GoogleApiException"/> errors from Google Cloud APIs.
/// </para>
/// <para>
/// <strong>Observability:</strong>
/// Integrates with Brighter's tracing via <see cref="IAmABrighterTracer"/> and provides structured logging through <see cref="ILogger"/>.
/// </para>
/// </remarks>
public partial class GcsLuggageStore(GcsLuggageOptions options) : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "gcp_gcs";
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<GcsLuggageStore>();
    private static readonly Dictionary<string, string> s_spanAttributes = new();
    
    /// <inheritdoc cref="IAmAStorageProvider.Tracer" />
    public IAmABrighterTracer? Tracer { get; set; }
    
    /// <inheritdoc />
    public async Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(options.ProjectId))
        {
            throw new ConfigurationException("Project ID not set");
        }

        if (string.IsNullOrEmpty(options.BucketName))
        {
            throw new ConfigurationException("Bucket name not set");
        }

        if (options.Strategy == StorageStrategy.Assume)
        {
            return;
        }

        var client = await options.CreateStorageClientAsync();
        try
        {
            _ = await client.GetBucketAsync(options.BucketName, options.GetBucketOptions, cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            if (options.Strategy == StorageStrategy.Validate)
            {
                throw new InvalidOperationException($"Bucket {options.BucketName} does not exist");
            }
            
            await client.CreateBucketAsync(options.ProjectId, options.BucketName, options.CreateBucketOptions, cancellationToken);
        }
        catch(Exception e)
        {
            Log.ErrorCreatingValidatingLuggageStore(s_logger, options.BucketName, e);
            throw;   
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes));
        try
        {
            var client = await options.CreateStorageClientAsync();
            await client.DeleteObjectAsync(options.BucketName, claimCheck, options.DeleteObjectOptions, cancellationToken);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            Log.CouldNotDeleteLuggage(s_logger, claimCheck, options.BucketName);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes));
        try
        {
            var client = await options.CreateStorageClientAsync();
            var obj = await client.GetObjectAsync(options.BucketName, claimCheck, options.GetObjectOptions, cancellationToken);

            var stream = new MemoryStream();
            await client.DownloadObjectAsync(obj, stream, options.DownloadObjectOptions, cancellationToken);
            stream.Position = 0;
            return stream;
        }
        catch (Exception e)
        {
            Log.UnableToRead(s_logger, claimCheck, options.BucketName, e);
            throw;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes));
        try
        {
            var client = await options.CreateStorageClientAsync();
            var obj = await client.GetObjectAsync(options.BucketName, claimCheck, options.GetObjectOptions, cancellationToken);
            return obj != null;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var prefix = options.Prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            prefix += "/";
        }
        
        var claimCheck = $"{prefix}{Guid.NewGuid().ToString()}";
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes, stream.Length));
        try
        {
            var client = await options.CreateStorageClientAsync();
            await client.UploadObjectAsync(options.BucketName, 
                claimCheck, 
                "application/vnd.brighter.claim-check",
                stream,
                options.UploadObjectOptions,
                cancellationToken);
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void EnsureStoreExists()
    {
        if (string.IsNullOrEmpty(options.ProjectId))
        {
            throw new ConfigurationException("Project ID not set");
        }

        if (string.IsNullOrEmpty(options.BucketName))
        {
            throw new ConfigurationException("Bucket name not set");
        }

        if (options.Strategy == StorageStrategy.Assume)
        {
            return;
        }

        var client = options.CreateStorageClient();
        try
        {
            _ = client.GetBucket(options.BucketName, options.GetBucketOptions);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            if (options.Strategy == StorageStrategy.Validate)
            {
                throw new InvalidOperationException($"Bucket {options.BucketName} does not exist");
            }
            
            client.CreateBucket(options.ProjectId, options.BucketName, options.CreateBucketOptions);
        }
    }

    /// <inheritdoc />
    public void Delete(string claimCheck)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes));
        try
        {
            var client =  options.CreateStorageClient();
            client.DeleteObject(options.BucketName, claimCheck, options.DeleteObjectOptions);
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            Log.CouldNotDeleteLuggage(s_logger, claimCheck, options.BucketName);
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public Stream Retrieve(string claimCheck)
    { 
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes)); 
        try
        {
            var client = options.CreateStorageClient();
            var obj = client.GetObject(options.BucketName, claimCheck, options.GetObjectOptions);

            var stream = new MemoryStream();
            client.DownloadObject(obj, stream, options.DownloadObjectOptions);
            stream.Position = 0;
            return stream;
        }
        catch (Exception e)
        {
            Log.UnableToRead(s_logger, claimCheck, options.BucketName, e);
            throw;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public bool HasClaim(string claimCheck)
    {
       var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes));
        try
        {
            var client = options.CreateStorageClient();
            var obj = client.GetObject(options.BucketName, claimCheck, options.GetObjectOptions);
            return obj != null;
        }
        catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public string Store(Stream stream)
    {
        var prefix = options.Prefix;
        if (!string.IsNullOrEmpty(prefix))
        {
            prefix += "/";
        }
        
        var claimCheck = $"{prefix}{Guid.NewGuid().ToString()}";
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, options.BucketName, claimCheck, s_spanAttributes, stream.Length));
        try
        {
            var client = options.CreateStorageClient();
            client.UploadObject(options.BucketName,
                claimCheck, 
                "application/vnd.brighter.claim-check", 
                stream,
                options.UploadObjectOptions);
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Error creating or validating luggage store {BucketName}")]
        public static partial void ErrorCreatingValidatingLuggageStore(ILogger logger, string bucketName, Exception e);

        [LoggerMessage(LogLevel.Error, "Could not delete luggage with claim {ClaimCheck} from {Bucket}")]
        public static partial void CouldNotDeleteLuggage(ILogger logger, string claimCheck, string bucket);

        [LoggerMessage(LogLevel.Information, "Downloading {ClaimCheck} from {Bucket}")]
        public static partial void Downloading(ILogger logger, string claimCheck, string bucket);

        [LoggerMessage(LogLevel.Error, "Could not download {ClaimCheck} from {BucketName}")]
        public static partial void CouldNotDownload(ILogger logger, string claimCheck, string bucketName);

        [LoggerMessage(LogLevel.Error, "Unable to read {ClaimCheck} from {Bucket}")]
        public static partial void UnableToRead(ILogger logger, string claimCheck, string bucket,  Exception exception);

        [LoggerMessage(LogLevel.Information, "Uploading {ClaimCheck} to {Bucket}")]
        public static partial void Uploading(ILogger logger, string claimCheck, string bucket);
    }
}
