using System;
using System.Collections.Generic;
using System.Linq;
using Paramore.Adapters.Infrastructure.Repositories;

namespace Paramore.Adapters.Tests.UnitTests.fakes
{
    internal class FakeRepository<T, TDocument> : IRepository<T, TDocument>
        where T : IAmAnAggregateRoot<TDocument>, new()
        where TDocument : IAmADocument
    {
        readonly List<T> members = new List<T>();

        #region IRepository<T,TDocument> Members

        public void Add(T aggregate)
        {
            members.Add(aggregate);
        }

        public T this[System.Guid id]
        {
            get
            {
                return  members.SingleOrDefault(member => member.Id == id);
            }
        }

        public IUnitOfWork UnitOfWork
        {
            set { ; }
        }

        public void Delete(T aggregate)
        {
            members.RemoveAll(member => member.Id == aggregate.Id);
        }

        #endregion
    }
}