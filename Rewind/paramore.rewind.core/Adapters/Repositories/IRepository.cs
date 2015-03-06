using System;

namespace Paramore.Rewind.Core.Adapters.Repositories
{
    public interface IRepository<T, TDocument> where T : IAmAnAggregateRoot<TDocument> where TDocument : IAmADocument
    {
        T this[Guid id] { get; }
        IUnitOfWork UnitOfWork { set; }
        void Add(T aggregate);
        void Delete(T aggregate);
    }
}