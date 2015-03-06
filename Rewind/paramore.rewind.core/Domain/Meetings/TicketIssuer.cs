using Paramore.Rewind.Core.Domain.Venues;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public class TicketIssuer : IIssueTickets
    {
        public Tickets Issue(Capacity capacity)
        {
            return new Tickets(capacity);
        }
    }
}