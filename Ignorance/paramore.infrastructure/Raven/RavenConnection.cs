using Castle.MicroKernel;
using Paramore.Infrastructure.Domain;
using Raven.Client;

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

    }
}
