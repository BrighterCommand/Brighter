using System;

namespace Paramore.Infrastructure.Domain
{
    public interface IUnitOfWork : IDisposable
    {
        void Add(dynamic entity);
        void Commit();
    }
}