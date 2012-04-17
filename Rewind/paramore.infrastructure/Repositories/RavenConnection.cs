using System.Configuration;
using System.IO;
using Raven.Client.Document;

namespace Paramore.Infrastructure.Repositories
{
    public static class RavenConnection
    {
        public static DocumentStore CreateDatabase()
        {
            var store = new DocumentStore {Url = ConfigurationManager.AppSettings["RavenServer"]};
            store.Initialize();
            //IndexCreation.CreateIndexes(typeof(EventSeries_ByName).Assembly, store);
            return store;
        }

        public static void ClearDatabase(string ravenPath)
        {
            Directory.Delete(ravenPath);
        }

    }

 
}
