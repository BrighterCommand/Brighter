using System;
using System.IO;
using System.Threading.Tasks;
using Paramore.Brighter.Transforms.Storage;

namespace Paramore.Brighter.Tranformers.AWS
{

    public class S3LuggageStore : IAmAStorageProviderAsync
    {
        public async Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public async Task<Stream> DownloadAsync(Guid claimCheck)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> HasClaim(Guid id)
        {
            throw new NotImplementedException();
        }

        public async Task<Guid> UploadAsync(Stream stream)
        {
            throw new NotImplementedException();
        }
    }
}
