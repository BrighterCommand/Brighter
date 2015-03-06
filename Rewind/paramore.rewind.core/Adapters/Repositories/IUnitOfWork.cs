using System;
using Raven.Client.Linq;

namespace Paramore.Rewind.Core.Adapters.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        void Add(dynamic entity);
        void Delete(dynamic entity);
        T Load<T>(Guid id);
        IRavenQueryable<T> Query<T>();
        void Commit();
    }
}