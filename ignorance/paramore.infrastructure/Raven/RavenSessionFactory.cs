using Paramore.Infrastructure.Domain;
using Raven.Client.Document;

namespace Paramore.Infrastructure.Raven
{
    public class UnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        private readonly DocumentStore _documentStore;

        public UnitOfWorkFactory(DocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(_documentStore.OpenSession());
        }
    }
}