using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
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

    public class S3LuggageStore : IAmAStorageProviderAsync, IDisposable
    {
        private string _accountId;
        private string _bucketName;
        private IAmazonS3 _client;
        private AsyncRetryPolicy _policy;

        private static readonly ILogger s_logger = ApplicationLogging.CreateLogger<S3LuggageStore>();
        private string _luggagePrefix;

        private S3LuggageStore() { }

        /// <summary>
        /// Creates an S3 Luggage Store
        /// Note that if you set S3LuggageStoreCreation.CreateIfMissing or ValidateExists the factor will make HTTP calls to
        /// AWS to check for and/or create the bucket. This is expensive as an operation.
        /// For this reason we recommend that you use the S3LuggageStore with a singleton scope from your IoC container.
        /// Because of this, the S3LuggageStore should hold no state that is not thread-safe.
        /// This factory will throw if exceptions occur during creation 
        /// </summary>
        /// <param name="client">An Amazon S3 client to use to connect to S3</param>
        /// <param name="bucketName">The bucket to store luggage in</param>
        /// <param name="bucketRegion">The region of the bucket</param>
        /// <param name="tags">Tags to use if creating the bucket</param>
        /// <param name="acl">The Access Control when creating a bucket</param>
        /// <param name="policy">The Polly policy to wrap around S3 calls:
        ///  - Catch the InvalidOperationException to retry an invalid response i.e. not 200
        ///  - Otherwise catch an AmazonS3Exception to catch network errors
        ///  - If no policy is passed we will use a default policy to catch and retry an AmazonS3Exception
        ///  - Immediate first retry, then at 50 and 100ms 
        ///  This is not applied to the HttpClientFactory instance used to check for existence you must set that
        /// </param>
        /// <param name="storeCreation">Whether we should create, validate or assume the luggage store exists</param>
        /// <param name="stsClient">An AmazonSecurityTokenServiceClient, required unless you assume luggage store exists </param>
        /// <param name="abortFailedUploadsAfterDays">After what delay (in days) should we delete failed uploads. Default is 1</param>
        /// <param name="deleteGoodUploadsAfterDays">After what delay (in days) should we delete successful uploads. Default is 3, -1 is do not auto-delete</param>
        /// <param name="luggagePrefix">What prefix should the deletion policy be applied to: defaults to BRIGHTER_CHECKED_LUGGAGE</param>
        public static async Task<S3LuggageStore> CreateAsync(
            IAmazonS3 client,
            string bucketName,
            S3LuggageStoreCreation storeCreation,
            IHttpClientFactory httpClientFactory,
            IAmazonSecurityTokenService stsClient = null,
            S3Region bucketRegion = null,
            List<Tag> tags = null,
            S3CannedACL acl = null,
            AsyncRetryPolicy policy = null,
            int abortFailedUploadsAfterDays = 1,
            int deleteGoodUploadsAfterDays = 3,
            string luggagePrefix = "BRIGHTER_CHECKED_LUGGAGE"
        )
        {
            var luggageStore = new S3LuggageStore();
            luggageStore._client = client;
            luggageStore._bucketName = bucketName;
            luggageStore._luggagePrefix = luggagePrefix;

            if (policy == null) policy = GetDefaultS3Policy();
            luggageStore._policy = policy;

            if (storeCreation == S3LuggageStoreCreation.CreateIfMissing || storeCreation == S3LuggageStoreCreation.ValidateExists)
            {
                luggageStore._accountId = await GetAccountIdAsync(stsClient);
                var bucketExists = await BucketExistsAsync(httpClientFactory, luggageStore._accountId, bucketName, bucketRegion);
                if (!bucketExists && storeCreation == S3LuggageStoreCreation.CreateIfMissing)
                {
                    CreateBucketAsync(
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

            return luggageStore;
        }

       ~S3LuggageStore()
        {
            ReleaseUnmanagedResources();
        }

        public async Task DeleteAsync(string id, CancellationToken cancellationToken = default(CancellationToken))
        {
        }

        public async Task<Stream> DownloadAsync(string claimCheck, CancellationToken cancellationToken = default(CancellationToken))
        {
            return null;
        }

        public async Task<bool> HasClaimAsync(string id, CancellationToken cancellationToken = default(CancellationToken))
        {
            return false;
        }

        public async Task<string> UploadAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            var claim = $"{_luggagePrefix}{Guid.NewGuid().ToString()}";
            var transferUtility = new TransferUtility(_client);
            transferUtility.UploadAsync(stream, _bucketName, claim, cancellationToken);
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
            using (var headRequest = new HttpRequestMessage(HttpMethod.Head, $"{bucketName}.s3.{bucketRegion.Value}.amazonaws.com"))
            {
                headRequest.Headers.Add("x-amz-expected-bucket-owner", accountId);
                var response = await httpClient.SendAsync(headRequest);
                return response.IsSuccessStatusCode;
            }
        }

        private static async void CreateBucketAsync(IAmazonS3 client,
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
            if (string.IsNullOrEmpty(bucketName)) throw new ArgumentNullException(nameof(bucketName), "We require a bucket name for the luggage store, none was supplied");
            if (region == null) throw new ArgumentNullException(nameof(region), "We require an S3 Region to create the luggage store bucket in");
            if (cannedAcl == null) throw new ArgumentNullException(nameof(cannedAcl), "We require Acls to be set for the luggage store bucket");

            await asyncRetryPolicy.ExecuteAsync(async () => 
            {
                var bucketRequest = new PutBucketRequest { BucketName = bucketName, BucketRegion = region, CannedACL = cannedAcl };
                var createBucketResponse = await client.PutBucketAsync(bucketRequest);
                if (createBucketResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"Could not create {bucketName} on {region}");
            });

            await asyncRetryPolicy.ExecuteAsync(async () =>
            {
                var rules = new List<LifecycleRule>();
                var lifeCycleRule = new LifecycleRule
                {
                    AbortIncompleteMultipartUpload = new LifecycleRuleAbortIncompleteMultipartUpload { DaysAfterInitiation = abortFailedUploadsAfterDays },
                    Filter = new LifecycleFilter { LifecycleFilterPredicate = new LifecyclePrefixPredicate { Prefix = luggagePrefix } }
                };
                if (deleteGoodUploadsAfterDays != -1) lifeCycleRule.Expiration = new LifecycleRuleExpiration { Days = deleteGoodUploadsAfterDays };

                var lifeCycleRequest = new PutLifecycleConfigurationRequest
                {
                    BucketName = bucketName, ExpectedBucketOwner = accountId, Configuration = new LifecycleConfiguration { Rules = rules }
                };
                await client.PutLifecycleConfigurationAsync(lifeCycleRequest);
            });
            
            if (tags != null)
            {
                await asyncRetryPolicy.ExecuteAsync(async () =>
                {
                    var putBucketTagging = new PutBucketTaggingRequest { BucketName = bucketName, ExpectedBucketOwner = accountId, TagSet = tags };

                    var taggingResponse = await client.PutBucketTaggingAsync(putBucketTagging);
                    if (taggingResponse.HttpStatusCode != HttpStatusCode.OK) throw new InvalidOperationException($"Could not add tags to {bucketName}");
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
            var delay = Backoff.ConstantBackoff(TimeSpan.FromMilliseconds(50), retryCount: 3, fastFirst:true);

            return Policy
                .Handle<AmazonS3Exception>()
                .WaitAndRetryAsync(delay);
        }
  
     }
}
