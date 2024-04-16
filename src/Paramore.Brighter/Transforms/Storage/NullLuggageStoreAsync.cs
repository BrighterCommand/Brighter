using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Paramore.Brighter.Transforms.Storage
{
    public class NullLuggageStoreAsync : IAmAStorageProviderAsync
    {
        public Task DeleteAsync(string claimCheck, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public Task<Stream> RetrieveAsync(string claimCheck, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public Task<bool> HasClaimAsync(string claimCheck, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public Task<string> StoreAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }
    }
}
