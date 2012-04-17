using System.Linq;
using Machine.Specifications;
using Paramore.Domain.Entities.Meetings;
using Paramore.Domain.Factories;
using Paramore.Domain.ValueTypes;

namespace Paramore.Tests.domain.Meetings
{
    public class When_Issuing_Tickets
    {
        static readonly TicketIssuer ticketIssuer = new TicketIssuer();
        const int NUMBER_OF_TICKETS = 50;
        static Tickets tickets;
        
        Because of = () => tickets = ticketIssuer.Issue(new Capacity(NUMBER_OF_TICKETS));

        It should_issue_the_requested_number_of_tickets = () => tickets.Count().ShouldEqual(NUMBER_OF_TICKETS);
    }
}
