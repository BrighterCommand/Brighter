using System.Configuration;
using Paramore.Infrastructure.Domain;
using Raven.Client;
using Raven.Client.Document;

namespace Paramore.Infrastructure.Raven
{
    public class UnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        private static IDocumentStore _documentStore;

        public static IDocumentStore DocumentStore
        {
            get { return (_documentStore ?? (_documentStore = CreateDocumentStore())); }
        }

        private static IDocumentStore CreateDocumentStore()
        {
            var store = new DocumentStore() {Url = ConfigurationManager.AppSettings["RavenServer"]};
            store.Initialize();

            return store;
        }
        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(DocumentStore.OpenSession());
        }
    }
}