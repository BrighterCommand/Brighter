using System.IO;

namespace Paramore.Brighter.Transforms.Storage
{
    public class NullLuggageStore : IAmAStorageProvider
    {
        public void Delete(string claimCheck)
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public Stream Retrieve(string claimCheck)
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public bool HasClaim(string claimCheck)
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }

        public string Store(Stream stream)
        {
            throw new System.NotImplementedException("This is a null store, you must register a real store after Brighter");
        }
    }
}
