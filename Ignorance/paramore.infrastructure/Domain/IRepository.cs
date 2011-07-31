using System;

namespace Paramore.Infrastructure.Domain
{
    public interface IRepository<T> where T: IAggregateRoot
    {
        void Add(T aggregate);
        T this[Guid id] { get; }
    }
}
