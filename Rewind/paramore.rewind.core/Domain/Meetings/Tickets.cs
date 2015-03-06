using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Paramore.Rewind.Core.Domain.Venues;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public class Tickets : IEnumerable<Ticket>
    {
        private readonly IList<Ticket> _tickets = new List<Ticket>();

        public Tickets(Capacity capacity)
        {
            for (int i = 1; i <= capacity; i++)
            {
                _tickets.Add(new Ticket());
            }
        }

        public Tickets(IEnumerable<MeetingDocumentTickets> tickets)
        {
            _tickets = tickets.Select(ticket => new Ticket()).ToList();
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

        #region Pesistence

        public List<MeetingDocumentTickets> ToDocumentSections()
        {
            return _tickets.Select(ticket => ticket.ToDocumentSection()).ToList();
        }

        #endregion
    }
}