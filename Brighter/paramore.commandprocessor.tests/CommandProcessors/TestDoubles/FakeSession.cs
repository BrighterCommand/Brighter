using System.Collections.Generic;
using System.Linq;

namespace paramore.commandprocessor.tests.CommandProcessors.TestDoubles
{
    public class FakeSession : IUnitOfWork
    {
        readonly IList<object> _identityMap = new List<object>(); 

        public void Add<T>(T aggregate)
        {
            _identityMap.Add(aggregate);
        }

        public void Commit() {}

        public T Load<T>(int id) where T : IAmAnAggregate
        {
            return _identityMap.Cast<T>().Where(aggregate => aggregate.ID == id).Select(aggregate => aggregate).FirstOrDefault();

        }

        public IEnumerable<T> Query<T>() where T : IAmAnAggregate
        {
            return _identityMap.Cast<T>();
        }
    }
}
