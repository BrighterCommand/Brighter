using Azure.Core;
using Azure.Storage.Blobs;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Transformers.Azure
{
    public class AzureBlobLuggageStore : IAmAStorageProviderAsync
    {
        private readonly BlobContainerClient _blockBlobClient;
        public AzureBlobLuggageStore(Uri containterUrl, TokenCredential tokenCredential)
        {
            _blockBlobClient = new BlobContainerClient(containterUrl, tokenCredential);
        }

        /// <summary>
        /// Delete the luggage identified by the claim check
        /// Used to clean up after luggage is retrieved
        /// </summary>
        /// <param name="claimCheck">The claim check for the luggage</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken)
        {
            var blobClient = _blockBlobClient.GetBlobClient(claimCheck);

            return blobClient.DeleteAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Downloads the luggage associated with the claim check
        /// </summary>
        /// <param name="claimCheck">The claim check for the luggage</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The luggage as a stream</returns>
        public async Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken)
        {
            var blobClient = _blockBlobClient.GetBlobClient(claimCheck);

            var response = await blobClient.ExistsAsync(cancellationToken);

            return response.Value;
        }

        /// <summary>
        /// Do we have luggage for this claim check - in case of error or deletion
        /// </summary>
        /// <param name="claimCheck"></param>
        /// <param name="cancellationToken">The cancellation token</param>
        public Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken)
        {
            var blobClient = _blockBlobClient.GetBlobClient(claimCheck);

            return blobClient.OpenReadAsync(cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Puts luggage into the store and provides a claim check for that luggage
        /// </summary>
        /// <param name="stream">A stream representing the luggage to check</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>A claim check for the luggage stored</returns>
        public async Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken)
        {
            var claimId = Guid.NewGuid().ToString();

            var blobClient = _blockBlobClient.GetBlobClient(claimId);

            await blobClient.UploadAsync(stream, cancellationToken);

            return claimId;
        }
    }
}
