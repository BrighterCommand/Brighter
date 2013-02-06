using System;
using Raven.Abstractions.Commands;
using Raven.Client;
using Raven.Client.Linq;

namespace Paramore.Adapters.Infrastructure.Repositories
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
            Type entityType = entity.GetType();
            var entityDocumentTypeName = entityType.Name;
            var key = string.Format(@"{0}s/{1}", entityDocumentTypeName, entity.Id.ToString());

            session.Advanced.Defer(new DeleteCommandData{Key = key});
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