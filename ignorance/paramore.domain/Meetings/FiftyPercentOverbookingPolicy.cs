using System;

namespace Paramore.Domain.Meetings
{
    internal class FiftyPercentOverbookingPolicy : IOverbookingPolicy
    {
        private readonly ITicketIssuer _ticketIssuer;

        public FiftyPercentOverbookingPolicy(ITicketIssuer ticketIssuer)
        {
            _ticketIssuer = ticketIssuer;
        }

        public Tickets AllocateTickets(Capacity capacity)
        {
            var totalTickets = new Capacity(Convert.ToInt32((int)capacity * 1.5));
            return _ticketIssuer.Issue(capacity);
        }
    }
}