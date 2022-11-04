using System;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Paramore.Brighter.Transforms.Storage;
using Polly.Retry;

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
        /// <summary>
        /// Creates an S3 Luggage Store
        /// Note that the constructor uses a Resource Acquisition is Initialization (RAII) strategy if you set S3LuggageStoreCreation.CreateIfMissing
        /// This is expensive as an operation and will throw if it fails.
        /// For this reason we recommend that you use the S3LuggageStore with a singleton scope from your IoC container.
        /// Because of this, the S3LuggageStore should hold no state that is not thread-safe 
        /// </summary>
        /// <param name="bucketName"></param>
        /// <param name="bucketRegion"></param>
        /// <param name="tags"></param>
        /// <param name="acl"></param>
        /// <param name="policy"></param>
        /// <param name="storeCreation"></param>
        public S3LuggageStore(
            IAmazonS3 client,
            string bucketName,
            S3Region bucketRegion,
            string[] tags,
            S3CannedACL acl,
            AsyncRetryPolicy policy,
            S3LuggageStoreCreation storeCreation
        )
        {
        }

        ~S3LuggageStore()
        {
            ReleaseUnmanagedResources();
        }

        public async Task DeleteAsync(Guid id)
        {
        }

        public async Task<Stream> DownloadAsync(Guid claimCheck)
        {
            return null;
        }

        public async Task<bool> HasClaimAsync(Guid id)
        {
            return false;
        }

        public async Task<Guid> UploadAsync(Stream stream)
        {
            return Guid.Empty;
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
    }
}
