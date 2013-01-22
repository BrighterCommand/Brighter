using Raven.Client.Document;

namespace Paramore.Adapters.Infrastructure.Repositories
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