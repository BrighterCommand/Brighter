using System;
using Paramore.Infrastructure.Raven;

namespace Paramore.Infrastructure.Domain
{
    public interface IRepository<T, TDataObject> where T: IAmAnAggregateRoot<TDataObject> where TDataObject : IAmADataObject
    {
        void Add(T aggregate);
        T this[Guid id] { get; }
        IUnitOfWork UnitOfWork { set; }
    }
}
