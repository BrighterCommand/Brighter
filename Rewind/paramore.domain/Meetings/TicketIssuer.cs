using Paramore.Domain.Venues;

namespace Paramore.Domain.Meetings
{
    public class TicketIssuer : IIssueTickets
    {
        public Tickets Issue(Capacity capacity)
        {
            return new Tickets(capacity);
        }
    }
}