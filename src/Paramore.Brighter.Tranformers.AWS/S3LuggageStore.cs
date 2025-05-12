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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using Microsoft.Extensions.Logging;
using Paramore.Brighter.Logging;
using Paramore.Brighter.Transforms.Storage;
using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Retry;
using Tag = Amazon.S3.Model.Tag;

namespace Paramore.Brighter.Tranformers.AWS
{
    /// <summary>
    /// What responsibility do we have for infrastructure creation, if any
    /// </summary>
    public enum S3LuggageStoreCreation
    {
        /// <summary>
        /// We should create the bucket used by the luggage store if it does not exist
        /// </summary>
        CreateIfMissing,

        /// <summary>
        /// We should validate that the bucket exists and throw a ConfigurationException if not
        /// </summary>
        ValidateExists,

        /// <summary>
        /// We should assume the bucket has been created successfully and not check for it
        /// </summary>
        AssumeExists
    }

    public partial class S3LuggageStore : IAmAStorageProviderAsync, IDisposable
    {
        private string _accountId;
        private string _bucketName;
        private IAmazonS3 _client;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<S3LuggageStore>();
        private string _luggagePrefix;
        private static readonly Regex s_validBucketNameRx;

        static S3LuggageStore()
        {
            string pattern = @"(?!(^xn--|.+-s3alias$))^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$";
            s_validBucketNameRx = new Regex(pattern, RegexOptions.Compiled);
        }

        private S3LuggageStore() { }

        /// <summary>
        /// Creates an S3 Luggage Store
        /// Note that if you set S3LuggageStoreCreation.CreateIfMissing or ValidateExists the factor will make HTTP calls to
        /// AWS to check for and/or create the bucket. This is expensive as an operation.
        /// For this reason we recommend that you use the S3LuggageStore with a singleton scope from your IoC container.
        /// Because of this, the S3LuggageStore should hold no state that is not thread-safe.
        /// This factory will throw if exceptions occur during creation 
        /// </summary>
        /// <param name="client">An Amazon S3 client to use to connect to S3. If you need to ensure that objects uploaded are client-side encrypted use AmazonS3EncryptionClientV2</param>
        /// <param name="bucketName">The bucket to store luggage in. The name must follows S3 bucket name rules: https://docs.aws.amazon.com/AmazonS3/latest/userguide/bucketnamingrules.html</param>
        /// <param name="storeCreation">Whether we should create, validate or assume the luggage store exists. Defaults to assumes exists</param>
        /// <param name="httpClientFactory">We need an HTTP client to check whether a bucket exists.</param>
        /// <param name="stsClient">An AmazonSecurityTokenServiceClient, required unless you assume luggage store exists </param>
        /// <param name="bucketRegion">The region of the bucket. This MUST match the region of the client.</param>
        /// <param name="tags">Tags to use if creating the bucket</param>
        /// <param name="acl">The Access Control when creating a bucket</param>
        /// <param name="policy">The Polly policy to wrap around S3 calls:
        ///     - Catch the InvalidOperationException to retry an invalid response i.e. not 200
        ///     - Otherwise catch an AmazonS3Exception to catch network errors
        ///     - If no policy is passed we will use a default policy to catch and retry an AmazonS3Exception
        ///     - Immediate first retry, then at 50 and 100ms 
        ///     This is not applied to the HttpClientFactory instance used to check for existence you must set that
        /// </param>
        /// <param name="abortFailedUploadsAfterDays">After what delay (in days) should we delete failed uploads. Default is 1</param>
        /// <param name="deleteGoodUploadsAfterDays">After what delay (in days) should we delete successful uploads. Default is 3, -1 is do not auto-delete</param>
        /// <param name="luggagePrefix">What prefix should the deletion policy be applied to: defaults to BRIGHTER_CHECKED_LUGGAGE</param>
        public static async Task<S3LuggageStore> CreateAsync(IAmazonS3 client,
            string bucketName,
            S3LuggageStoreCreation storeCreation = S3LuggageStoreCreation.AssumeExists,
            IHttpClientFactory httpClientFactory = null,
            IAmazonSecurityTokenService stsClient = null,
            S3Region bucketRegion = null,
            List<Tag> tags = null,
            S3CannedACL acl = null,
            AsyncRetryPolicy policy = null,
            int abortFailedUploadsAfterDays = 1,
            int deleteGoodUploadsAfterDays = 3,
            string luggagePrefix = "BRIGHTER_CHECKED_LUGGAGE")
        {
            if (client == null) throw new ArgumentNullException(nameof(client), "We need a valid S3 client to connect to AWS, but was null");
            if (string.IsNullOrEmpty(bucketName))
                throw new ArgumentNullException(nameof(bucketName), "We require the name of a bucket to use for the luggage store");
            if (!s_validBucketNameRx.IsMatch(bucketName)) throw new ArgumentException("The bucketName does not match S3 naming rules", nameof(bucketName));

            if (storeCreation == S3LuggageStoreCreation.ValidateExists || storeCreation == S3LuggageStoreCreation.CreateIfMissing)
            {
                if (stsClient == null)
                    throw new ArgumentNullException(nameof(stsClient),
                        "To check for the existence of your luggage store bucket we use the IAmazonSecurityServiceToken to get your account id, and it cannot be null");

                if (httpClientFactory == null)
                    throw new ArgumentNullException(nameof(httpClientFactory),
                        "To check for the existence of your luggage store bucket we use HttpClient, so we require an IHttpClientFactory and it cannot be null");

                if (bucketRegion == null)
                    throw new ArgumentNullException(
                        nameof(bucketRegion), "We need to know which region to create the bucket in, it should match the client region");
            }

            if (storeCreation == S3LuggageStoreCreation.CreateIfMissing)
            {
                if (acl == null)
                    throw new ArgumentNullException(
                        nameof(acl), "We need to lock down any bucket we create with an ACL");
            }

            var luggageStore = new S3LuggageStore();
            luggageStore._client = client;
            luggageStore._bucketName = bucketName;
            luggageStore._luggagePrefix = luggagePrefix;

            if (policy == null) policy = GetDefaultS3Policy();

            if (storeCreation == S3LuggageStoreCreation.CreateIfMissing || storeCreation == S3LuggageStoreCreation.ValidateExists)
            {
                try
                {
                    luggageStore._accountId = await GetAccountIdAsync(stsClient);
                    var bucketExists = await BucketExistsAsync(httpClientFactory, luggageStore._accountId, bucketName, bucketRegion);

                    if (!bucketExists)
                    {
                        if (storeCreation == S3LuggageStoreCreation.ValidateExists)
                            throw new InvalidOperationException($"There was no luggage store with the bucket {bucketName}");
                        else if (storeCreation == S3LuggageStoreCreation.CreateIfMissing)
                            await CreateBucketAsync(
                                client,
                                policy,
                                luggageStore._accountId,
                                bucketName,
                                bucketRegion,
                                acl,
                                tags,
                                abortFailedUploadsAfterDays,
                                deleteGoodUploadsAfterDays,
                                luggagePrefix);
                    }
                }
                catch (Exception e)
                {
                    Log.ErrorCreatingValidatingLuggageStore(s_logger, bucketName, bucketRegion, e);
                    throw;
                }
            }

            return luggageStore;
        }

        ~S3LuggageStore()
        {
            ReleaseUnmanagedResources();
        }

        public async Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default)
        {
            var request = new DeleteObjectRequest { BucketName = _bucketName, Key = claimCheck };

            var response = await _client.DeleteObjectAsync(request, cancellationToken);

            if (response.HttpStatusCode != HttpStatusCode.NoContent)
                Log.CouldNotDeleteLuggage(s_logger, claimCheck, _bucketName);
        }

        public async Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default)
        {
            var request = new GetObjectRequest { BucketName = _bucketName, Key = claimCheck, };

            Log.Downloading(s_logger, claimCheck, _bucketName);

            // Issue request and remember to dispose of the response
            GetObjectResponse response = await _client.GetObjectAsync(request, cancellationToken);
            if (response.HttpStatusCode != HttpStatusCode.OK)
            {
                Log.CouldNotDownload(s_logger, claimCheck, _bucketName);
                throw new InvalidOperationException($"Could not download {claimCheck} from {_bucketName}");
            }

            try
            {
                // Save object to local file
                var stream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(stream);
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
            finally
            {
                response.Dispose();
            }
        }

        public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new GetObjectMetadataRequest { BucketName = _bucketName, Key = claimCheck };
                var response = await _client.GetObjectMetadataAsync(request, cancellationToken);
                if (response.HttpStatusCode == HttpStatusCode.OK) return true;
            }
            catch (AmazonS3Exception s3Exception)
            {
                if (s3Exception.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return false;

                //status wasn't not found, so throw the exception
                throw;
            }

            return false;
        }

        public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            var claim = $"{_luggagePrefix}/luggage_store/{Guid.NewGuid().ToString()}";

            Log.Uploading(s_logger, claim, _bucketName);
            var transferUtility = new TransferUtility(_client);
            await transferUtility.UploadAsync(stream, _bucketName, claim, cancellationToken);
            return claim;
        }

        private void ReleaseUnmanagedResources()
        {
            // TODO release unmanaged resources here
        }

        public void Dispose()
        {
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        private static async Task<bool> BucketExistsAsync(IHttpClientFactory httpClientFactory, string accountId, string bucketName, S3Region bucketRegion)
        {
            var httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri($"https://{bucketName}.s3.{bucketRegion.Value}.amazonaws.com");
            using var headRequest = new HttpRequestMessage(HttpMethod.Head, @"/");
            headRequest.Headers.Add("x-amz-expected-bucket-owner", accountId);
            using var response = await httpClient.SendAsync(headRequest);
            //If we deny public access to the bucket, but it exists we get access denied; we get not-found if it does not exist 
            return (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Forbidden);
        }

        private static async Task CreateBucketAsync(
            IAmazonS3 client,
            AsyncRetryPolicy asyncRetryPolicy,
            string accountId,
            string bucketName,
            S3Region region,
            S3CannedACL cannedAcl,
            List<Tag> tags,
            int abortFailedUploadsAfterDays,
            int deleteGoodUploadsAfterDays,
            string luggagePrefix)
        {
            await asyncRetryPolicy.ExecuteAsync(async () =>
            {
                var bucketRequest = new PutBucketRequest
                {
                    BucketName = bucketName, BucketRegionName = region.Value, CannedACL = cannedAcl, UseClientRegion = false
                };
                var createBucketResponse = await client.PutBucketAsync(bucketRequest);
                if (createBucketResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"Could not create {bucketName} on {region}");
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
}

