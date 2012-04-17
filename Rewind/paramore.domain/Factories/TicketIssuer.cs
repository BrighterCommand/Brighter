using Paramore.Domain.Entities.Meetings;
using Paramore.Domain.ValueTypes;

namespace Paramore.Domain.Factories
{
    public class TicketIssuer : IIssueTickets
    {
        public Tickets Issue(Capacity capacity)
        {
            return new Tickets(capacity);
        }
    }
}