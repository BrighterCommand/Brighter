using System;
using Paramore.Infrastructure.Domain;

namespace Paramore.Infrastructure.Raven
{
    public class Repository<T, TDataObject> : IRepository<T, TDataObject> where T : IAmAnAggregateRoot<TDataObject>, new() where TDataObject : IAmADataObject
    {
        private readonly IUnitOfWork _unitOfWork;

        public Repository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public void Add(T aggregate)
        {
            TDataObject dto = aggregate.ToDTO();
            _unitOfWork.Add(dto);
        }

        public T this[Guid id]
        {
            get
            {
                var dataObject = _unitOfWork.Load<TDataObject>(id);
                var aggregate = new T();
                aggregate.Load(dataObject);
                return aggregate;
            }
        }
    }
}