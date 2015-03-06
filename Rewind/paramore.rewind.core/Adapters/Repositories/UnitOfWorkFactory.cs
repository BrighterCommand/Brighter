using Raven.Client.Document;

namespace Paramore.Rewind.Core.Adapters.Repositories
{
    public class UnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        private static DocumentStore documentStore;

        private static DocumentStore DocumentStore
        {
            get { return (documentStore ?? (CreateDocumentStore())); }
        }

        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(DocumentStore.OpenSession());
        }

        private static DocumentStore CreateDocumentStore()
        {
            documentStore = new DocumentStore {ConnectionStringName = "RavenServer"};
            documentStore.Initialize();

            return documentStore;
        }
    }
}