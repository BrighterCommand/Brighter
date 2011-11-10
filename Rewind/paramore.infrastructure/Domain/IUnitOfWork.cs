using System;
using Raven.Client.Linq;

namespace Paramore.Infrastructure.Domain
{
    public interface IUnitOfWork : IDisposable
    {
        void Add(dynamic entity);
        void Commit();
        T Load<T>(Guid id);
        IRavenQueryable<T> Query<T>();
    }
}