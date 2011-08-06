using Paramore.Infrastructure.Domain;
using Raven.Client;

namespace Paramore.Infrastructure.Raven
{
    public class UnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        private readonly IDocumentStore _documentStore;

        public UnitOfWorkFactory(IDocumentStore documentStore)
        {
            _documentStore = documentStore;
        }

        public IUnitOfWork CreateUnitOfWork()
        {
            return new UnitOfWork(_documentStore.OpenSession());
        }
    }
}