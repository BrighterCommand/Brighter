using System;

namespace Paramore.Domain.Meetings
{
    public class FiftyPercentOverbookingPolicy : IAmAnOverbookingPolicy
    {
        private readonly IIssueTickets _ticketIssuer;

        public FiftyPercentOverbookingPolicy(IIssueTickets ticketIssuer)
        {
            _ticketIssuer = ticketIssuer;
        }

        public Tickets AllocateTickets(Capacity capacity)
        {
            var total = new Capacity(Convert.ToInt32((int)capacity * 1.5));
            return _ticketIssuer.Issue(total);
        }
    }
}