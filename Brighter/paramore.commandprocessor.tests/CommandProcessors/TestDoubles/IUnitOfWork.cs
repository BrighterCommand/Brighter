using System;
using System.Collections.Generic;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    internal interface IUnitOfWork
    {
        void Add<T>(T aggregate);
        void Commit();
        T Load<T>(int id) where T : IAmAnAggregate;
        IEnumerable<T> Query<T>() where T : IAmAnAggregate;
    }
}