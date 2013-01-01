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

        public void Delete(dynamic entity)
        {
            session.Delete(entity);
        }

        public T Load<T>(Guid id)
        {
            return session.Load<T>(id);
        }

        public IRavenQueryable<T> Query<T>()
        {
            return session.Query<T>();
        }

        public void Commit()
        {
            session.SaveChanges();
        }

        public void Dispose()
        {
            session.Dispose();
        }
    }
}