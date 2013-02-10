using System;
using System.Diagnostics;

namespace Paramore.Adapters.Infrastructure.Repositories
{
    public class Repository<T, TDocument> : IRepository<T, TDocument> where T : IAmAnAggregateRoot<TDocument>, new() where TDocument : IAmADocument
    {
        public IUnitOfWork UnitOfWork { private get; set; }

        public void Add(T aggregate)
        {
            Debug.Assert(UnitOfWork != null);
            var dto = aggregate.ToDocument();
            UnitOfWork.Add(dto);
        }

        public T this[Guid id]
        {
            get
            {
                Debug.Assert(UnitOfWork != null);
                var dataObject = UnitOfWork.Load<TDocument>(id);
                var aggregate = new T();
                aggregate.Load(dataObject);
                return aggregate;
            }
        }

        public void Delete(T aggregate)
        {
            Debug.Assert(UnitOfWork != null);
            var dto = aggregate.ToDocument();
            UnitOfWork.Delete(dto);
        }
    }
}