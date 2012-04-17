using System;
using Raven.Client;
using Raven.Client.Linq;

namespace Paramore.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly IDocumentSession session;

        public UnitOfWork(IDocumentSession session)
        {
            this.session = session;
        }

        public void Add(dynamic entity)
        {
            session.Store(entity);
        }

        public void Commit()
        {
            session.SaveChanges();
        }

        public IRavenQueryable<T> Query<T>()
        {
            return session.Query<T>();
        }

        public T Load<T>(Guid id)
        {
            return session.Load<T>(id);
        }

        public void Dispose()
        {
            session.Dispose();
        }
    }
}