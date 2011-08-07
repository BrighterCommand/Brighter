using System;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;
using Raven.Client.Linq;

namespace Paramore.Tests.services.CommandProcessors.TestDoubles
{
    class MyUnitOfWorkFactory : IAmAUnitOfWorkFactory
    {
        public IUnitOfWork CreateUnitOfWork()
        {
             return new MyUnitOfWork();
        }
    }

    internal class MyUnitOfWork : IUnitOfWork
    {
        public void Dispose() {}
        public void Add(dynamic entity) { }
        public void Commit() { }
        public T Load<T>(Guid id)
        {
            throw new NotImplementedException();
        }

        public IRavenQueryable<T> Query<T>()
        {
            throw new NotImplementedException();
        }
    }
}
