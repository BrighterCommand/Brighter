using Castle.MicroKernel;
using Paramore.Infrastructure.Domain;
using Raven.Abstractions.Indexing;
using Raven.Client;
using System.IO;
using Raven.Client.Document;
using Raven.Client.Indexes;

namespace Paramore.Infrastructure.Raven
{
    public static class RavenConnection
    {
        public static IUnitOfWork GetUnitOfWork(IKernel kernel)
        {
            var factory = kernel.Resolve<IAmAUnitOfWorkFactory>();
            return factory.CreateUnitOfWork();
        }


        public static void DoInitialisation(IKernel kernel, IDocumentStore store)
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
