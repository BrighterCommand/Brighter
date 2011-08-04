using Paramore.Infrastructure.Domain;
using Raven.Client;

namespace Paramore.Infrastructure.Raven
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDocumentSession _session;

        public UnitOfWork(IDocumentSession session)
        {
            _session = session;
        }

        public void Add(dynamic entity)
        {
            _session.Store(entity);
        }

        public void Commit()
        {
            _session.SaveChanges();
        }

        public void Dispose()
        {
            _session.Dispose();
        }
    }
}