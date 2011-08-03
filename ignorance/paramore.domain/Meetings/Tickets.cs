using System.Collections;
using System.Collections.Generic;

namespace Paramore.Domain.Meetings
{
    public class Tickets : IEnumerable<Ticket>
    {
        private readonly IList<Ticket> _tickets = new List<Ticket>();

        public Tickets(Capacity capacity)
        {
            for(int i = 1;  i <= capacity; i++)
            {
                _tickets.Add(new Ticket());
            }
        }

        public int Count
        {
            get { return _tickets.Count; }
        }

        public IEnumerator<Ticket> GetEnumerator()
        {
            return _tickets.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}