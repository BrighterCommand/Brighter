using System.Configuration;
using Paramore.Infrastructure.Domain;
using Raven.Client;
using Raven.Client.Document;

namespace Paramore.Infrastructure.Raven
{
    public class UnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        private static DocumentStore documentStore;

        private static DocumentStore DocumentStore
        {
            get { return (documentStore ?? (CreateDocumentStore())); }
        }

        private static DocumentStore CreateDocumentStore()
        {
            documentStore = new DocumentStore() {ConnectionStringName = "RavenServer"};
            documentStore.Initialize();

            return documentStore;
        }
        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(DocumentStore.OpenSession());
        }
    }
}