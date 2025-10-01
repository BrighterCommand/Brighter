#region Licence
/* The MIT License (MIT)
Copyright © 2022 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
 
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Observability;
using Paramore.Brighter.Tasks;
using Paramore.Brighter.Transforms.Storage;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using Tag = Amazon.S3.Model.Tag;

namespace Paramore.Brighter.Transformers.AWS.V4;

/// <summary>
/// Implements a storage provider for the Brighter Claim Check pattern using Amazon S3.
/// This class handles the storing, retrieving, deleting, and checking existence of
/// message payloads (luggage) in an S3 bucket.
/// </summary>
/// <remarks>
/// This store leverages Amazon S3's object storage capabilities to manage large message payloads
/// efficiently and reliably. It provides both synchronous and asynchronous operations
/// conforming to Brighter's <see cref="IAmAStorageProvider"/> and <see cref="IAmAStorageProviderAsync"/> interfaces.
/// <para>
/// **Configuration:**
/// Instances of this class typically require configuration for the S3 bucket name,
/// AWS region, and AWS credentials (usually via environment variables, IAM roles,
/// or a configured <c>appsettings.json</c>).
/// </para>
/// <para>
/// **Disposal:**
/// This class implements <see cref="IDisposable"/> to ensure proper cleanup of
/// any underlying AWS S3 client resources when the store is no longer needed.
/// </para>
/// </remarks>
public partial class S3LuggageStore : IAmAStorageProvider, IAmAStorageProviderAsync
{
    private const string ClaimCheckProvider = "aws_s3";
    private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<S3LuggageStore>();
    private readonly S3LuggageOptions _options;
    private readonly Dictionary<string, string> _spanAttributes = new();
    private readonly string _bucketName;
    private readonly IAmazonS3 _client;
    private readonly string _luggagePrefix;

    /// <summary>
    /// Initializes a new instance of the <see cref="S3LuggageStore"/> class with the specified S3 luggage options.
    /// </summary>
    /// <param name="options">The <see cref="S3LuggageOptions"/> containing the S3 client, bucket details, and other configuration.</param>
    public S3LuggageStore(S3LuggageOptions options)
    {
        _client = options.Client;
        _luggagePrefix = options.LuggagePrefix;
        _options = options;
        _bucketName = options.BucketName;
        
        _spanAttributes["claim_check.aws-s3.region"] = options.BucketRegion.Value;
    }

    /// <inheritdoc cref="IAmAStorageProvider.Tracer"/>
    public IAmABrighterTracer? Tracer { get; set; }

    
    /// <inheritdoc />
    public async Task EnsureStoreExistsAsync(CancellationToken cancellationToken = default)
    {
        if (_options.Strategy == StorageStrategy.Assume)
        {
            return;
        }
        
        if(_options.HttpClientFactory == null)
        {
            throw new ConfigurationException("No HTTP Factory setup on S3Luggage Store");
        }
        
        try
        {
            var accountId = await GetAccountIdAsync(_options.StsClient);
            var bucketExists = await BucketExistsAsync(_options.HttpClientFactory, 
                accountId,
                _options.BucketName,
                _options.BucketRegion, 
                _options.BucketAddressTemplate);
        
            if (bucketExists)
            {
                return;
            }

            if (_options.Strategy == StorageStrategy.Validate)
            {
                throw new InvalidOperationException($"There was no luggage store with the bucket {_options.BucketName}");
            }

            if (_options.ACLs == null)
            {
                throw new ConfigurationException("No ACL setup on S3Luggage Store");
            }

            var policy = _options.RetryPolicy ?? GetDefaultS3Policy();

            await CreateBucketAsync(
                _options.Client,
                policy,
                accountId,
                _bucketName,
                _options.BucketRegion,
                _options.ACLs,
                _options.Tags,
                _options.TimeToAbortFailedUploads,
                _options.TimeToDeleteGoodUploads,
                _options.LuggagePrefix);
        }
        catch (Exception e)
        {
            Log.ErrorCreatingValidatingLuggageStore(s_logger, _bucketName, _options.BucketRegion, e);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Delete, ClaimCheckProvider, _bucketName, claimCheck, _spanAttributes));

        try
        {
            var request = new DeleteObjectRequest { BucketName = _bucketName, Key = claimCheck };
            var response = await _client.DeleteObjectAsync(request, cancellationToken);

            if (response.HttpStatusCode != HttpStatusCode.NoContent)
            {
                Log.CouldNotDeleteLuggage(s_logger, claimCheck, _bucketName);
            }
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Retrieve, ClaimCheckProvider, _bucketName, claimCheck, _spanAttributes));
        try
        {
            var request = new GetObjectRequest { BucketName = _bucketName, Key = claimCheck, };

            Log.Downloading(s_logger, claimCheck, _bucketName);

            // Issue request and remember to dispose of the response
            using var response = await _client.GetObjectAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.CouldNotDownload(s_logger, claimCheck, _bucketName);
                throw new InvalidOperationException($"Could not download {claimCheck} from {_bucketName}");
            }

            try
            {
                // Save object to local file
                var stream = new MemoryStream();
#if NETSTANDARD
                    await response.ResponseStream.CopyToAsync(stream);
#else
                await response.ResponseStream.CopyToAsync(stream, cancellationToken);
#endif
                stream.Position = 0;
                return stream;
            }
            catch (AmazonS3Exception)
            {
                Log.UnableToRead(s_logger, claimCheck, _bucketName);
                throw;
            }
            catch (Exception e) when (e is ObjectDisposedException || e is NotSupportedException)
            {
                Log.UnableToRead(s_logger, claimCheck, _bucketName);
                throw;
            }
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
    {
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.HasClaim, ClaimCheckProvider, _bucketName, claimCheck, _spanAttributes));
        try
        {
            var request = new GetObjectMetadataRequest { BucketName = _bucketName, Key = claimCheck };
            var response = await _client.GetObjectMetadataAsync(request, cancellationToken);
            return response.HttpStatusCode == HttpStatusCode.OK;
        }
        catch (AmazonS3Exception s3Exception)
        {
            if (s3Exception.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            //status wasn't not found, so throw the exception
            throw;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var claimCheck = $"{_luggagePrefix}/luggage_store/{Uuid.NewAsString()}";
        var span = Tracer?.CreateClaimCheckSpan(new ClaimCheckSpanInfo(ClaimCheckOperation.Store, ClaimCheckProvider, _bucketName, claimCheck, _spanAttributes, stream.Length));
        try
        {
            Log.Uploading(s_logger, claimCheck, _bucketName);
            var transferUtility = new TransferUtility(_client);
            await transferUtility.UploadAsync(stream, _bucketName, claimCheck, cancellationToken);
            return claimCheck;
        }
        finally
        {
            Tracer?.EndSpan(span);
        }
    }

    /// <inheritdoc />
    public void EnsureStoreExists() => BrighterAsyncContext.Run(async () => await EnsureStoreExistsAsync());

    /// <inheritdoc />
    public void Delete(string claimCheck) => BrighterAsyncContext.Run(async () => await DeleteAsync(claimCheck));

    /// <inheritdoc />
    public Stream Retrieve(string claimCheck) => BrighterAsyncContext.Run(async () => await RetrieveAsync(claimCheck));

    /// <inheritdoc />
    public bool HasClaim(string claimCheck) => BrighterAsyncContext.Run(async () => await HasClaimAsync(claimCheck));

    /// <inheritdoc />
    public string Store(Stream stream) => BrighterAsyncContext.Run(async () => await StoreAsync(stream));

    private static async Task<bool> BucketExistsAsync(IHttpClientFactory httpClientFactory, 
        string accountId, 
        string bucketName, 
        S3Region bucketRegion,
        string bucketAddressTemplate)
    {
        var httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri(bucketAddressTemplate
            .Replace("{BucketName}", bucketName)
            .Replace("{BucketRegion}", bucketRegion.Value)
        );
        
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, "/");
        headRequest.Headers.Add("x-amz-expected-bucket-owner", accountId);
        
        using var response = await httpClient.SendAsync(headRequest);
        //If we deny public access to the bucket, but it exists we get access denied; we get not-found if it does not exist 
        return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Forbidden;
    }

    private static async Task CreateBucketAsync(
        IAmazonS3 client,
        AsyncRetryPolicy asyncRetryPolicy,
        string accountId,
        string bucketName,
        S3Region region,
        S3CannedACL cannedAcl,
        List<Tag>? tags,
        int abortFailedUploadsAfterDays,
        int deleteGoodUploadsAfterDays,
        string luggagePrefix)
    {
        await asyncRetryPolicy.ExecuteAsync(async () =>
        {
            var bucketRequest = new PutBucketRequest
            {
                BucketName = bucketName, 
                BucketRegionName = region.Value,
                CannedACL = cannedAcl, 
                UseClientRegion = false
            };

            try
            {
                var createBucketResponse = await client.PutBucketAsync(bucketRequest);
                if (createBucketResponse.HttpStatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException($"Could not create {bucketName} on {region}");
                }
            }
            catch (BucketAlreadyOwnedByYouException)
            {
                // Ignoring this exception since it was created by another requests 
            }
            
        });

        await asyncRetryPolicy.ExecuteAsync(async () =>
        {
            var rules = new List<LifecycleRule>();
            var multipartLifeCycleRule = new LifecycleRule
            {
                AbortIncompleteMultipartUpload = new LifecycleRuleAbortIncompleteMultipartUpload { DaysAfterInitiation = abortFailedUploadsAfterDays },
                Filter = new LifecycleFilter { LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = luggagePrefix } },
                Id = "Aborted_multipart_uploads_delete",
                Status = LifecycleRuleStatus.Enabled
            };
            rules.Add(multipartLifeCycleRule);

            if (deleteGoodUploadsAfterDays != -1)
            {
                var goodUploadLifeCycleRule = new LifecycleRule
                {
                    Expiration = new LifecycleRuleExpiration { Days = deleteGoodUploadsAfterDays },
                    Filter = new LifecycleFilter { LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = luggagePrefix } },
                    Id = "Good_uploads_delete",
                    Status = LifecycleRuleStatus.Enabled
                };
                rules.Add(goodUploadLifeCycleRule);
            }

            var lifeCycleRequest = new PutLifecycleConfigurationRequest
            {
                BucketName = bucketName, ExpectedBucketOwner = accountId, Configuration = new LifecycleConfiguration { Rules = rules }
            };
            var lifeCycleResponse = await client.PutLifecycleConfigurationAsync(lifeCycleRequest);
            if (lifeCycleResponse.HttpStatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Could not add lifecycle rules to {bucketName}");
        });

        await asyncRetryPolicy.ExecuteAsync(async () =>
        {
            var blockAccessResponse = await client.PutPublicAccessBlockAsync(new PutPublicAccessBlockRequest
            {
                BucketName = bucketName,
                ExpectedBucketOwner = accountId,
                PublicAccessBlockConfiguration = new PublicAccessBlockConfiguration { BlockPublicPolicy = true, IgnorePublicAcls = true }
            });
            if (blockAccessResponse.HttpStatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Could not block public access to {bucketName}");
        });

        await asyncRetryPolicy.ExecuteAsync(async () =>
        {
            var ownershipControlsResponse = await client.PutBucketOwnershipControlsAsync(new PutBucketOwnershipControlsRequest
            {
                BucketName = bucketName,
                ExpectedBucketOwner = accountId,
                OwnershipControls = new OwnershipControls
                {
                    Rules = new List<OwnershipControlsRule> { new OwnershipControlsRule { ObjectOwnership = ObjectOwnership.BucketOwnerEnforced } }
                }
            });

            if (ownershipControlsResponse.HttpStatusCode != HttpStatusCode.OK)
                throw new InvalidOperationException($"Could not block public access to {bucketName}");
        });

        if (tags != null)
        {
            await asyncRetryPolicy.ExecuteAsync(async () =>
            {
                var putBucketTagging = new PutBucketTaggingRequest { BucketName = bucketName, ExpectedBucketOwner = accountId, TagSet = tags };

                var taggingResponse = await client.PutBucketTaggingAsync(putBucketTagging);
                if (!(taggingResponse.HttpStatusCode == HttpStatusCode.OK || taggingResponse.HttpStatusCode == HttpStatusCode.NoContent))
                    throw new InvalidOperationException($"Could not add tags to {bucketName}");
            });
        }
    }

    private static async Task<string> GetAccountIdAsync(IAmazonSecurityTokenService stsClient)
    {
        var callerIdentityResponse = await stsClient.GetCallerIdentityAsync(new GetCallerIdentityRequest());

        if (callerIdentityResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidOperationException("Could not find identity of AWS account");

        return callerIdentityResponse.Account;
    }

    private static AsyncRetryPolicy GetDefaultS3Policy()
    {
        var delay = Backoff.ConstantBackoff(TimeSpan.FromMilliseconds(50), retryCount: 3, fastFirst: true);

        return Policy
            .Handle<AmazonS3Exception>(e =>
            {
                switch (e.StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.BadGateway:
                    case HttpStatusCode.ServiceUnavailable:
                    case HttpStatusCode.GatewayTimeout:
                        return true;
                    default:
                        return false;
                }
            })
            .WaitAndRetryAsync(delay);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Error, "Error creating or validating luggage store {BucketName} in {BucketRegion}")]
        public static partial void ErrorCreatingValidatingLuggageStore(ILogger logger, string bucketName, S3Region bucketRegion, Exception e);

        [LoggerMessage(LogLevel.Error, "Could not delete luggage with claim {ClaimCheck} from {Bucket}")]
        public static partial void CouldNotDeleteLuggage(ILogger logger, string claimCheck, string bucket);

        [LoggerMessage(LogLevel.Information, "Downloading {ClaimCheck} from {Bucket}")]
        public static partial void Downloading(ILogger logger, string claimCheck, string bucket);

        [LoggerMessage(LogLevel.Error, "Could not download {ClaimCheck} from {BucketName}")]
        public static partial void CouldNotDownload(ILogger logger, string claimCheck, string bucketName);

        [LoggerMessage(LogLevel.Error, "Unable to read {ClaimCheck} from {Bucket}")]
        public static partial void UnableToRead(ILogger logger, string claimCheck, string bucket);
            
        [LoggerMessage(LogLevel.Information, "Uploading {ClaimCheck} to {Bucket}")]
        public static partial void Uploading(ILogger logger, string claimCheck, string bucket);
    }
}
