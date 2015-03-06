using System;
using Raven.Abstractions.Commands;
using Raven.Client;
using Raven.Client.Linq;

namespace Paramore.Rewind.Core.Adapters.Repositories
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
            string entityDocumentTypeName = entityType.Name;
            session.Advanced.DatabaseCommands.Delete(entityDocumentTypeName, entity.Id);
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