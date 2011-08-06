using System;
using Paramore.Infrastructure.Domain;
using Paramore.Infrastructure.Raven;

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
    }
}
