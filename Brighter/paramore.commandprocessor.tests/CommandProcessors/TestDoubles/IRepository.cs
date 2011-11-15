using System;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal interface IRepository<T> where T : IAmAnAggregate
    {
        void Add(T aggregate);
        T this[Guid id] { get; }
        IUnitOfWork UnitOfWork { set; }
    }
}