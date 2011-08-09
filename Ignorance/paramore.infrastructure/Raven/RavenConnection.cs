using Raven.Client;
using System.IO;

namespace Paramore.Infrastructure.Raven
{
    public static class RavenConnection
    {
        public static void DoInitialisation(IDocumentStore store)
        {
            store.Initialize();
            //IndexCreation.CreateIndexes(typeof(EventSeries_ByName).Assembly, store);

        }

        public static void ClearDatabase(string ravenPath)
        {
            Directory.Delete(ravenPath);
        }

    }

 
}
