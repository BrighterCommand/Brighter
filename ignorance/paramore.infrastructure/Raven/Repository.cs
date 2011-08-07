using System;
using System.Diagnostics;
using Paramore.Infrastructure.Domain;

namespace Paramore.Infrastructure.Raven
{
    public class Repository<T, TDataObject> : IRepository<T, TDataObject> where T : IAmAnAggregateRoot<TDataObject>, new() where TDataObject : IAmADataObject
    {
        public IUnitOfWork UnitOfWork { private get; set; }

        public void Add(T aggregate)
        {
            Debug.Assert(UnitOfWork != null);
            TDataObject dto = aggregate.ToDTO();
            UnitOfWork.Add(dto);
        }

        public T this[Guid id]
        {
            get
            {
                Debug.Assert(UnitOfWork != null);
                var dataObject = UnitOfWork.Load<TDataObject>(id);
                var aggregate = new T();
                aggregate.Load(dataObject);
                return aggregate;
            }
        }
    }
}