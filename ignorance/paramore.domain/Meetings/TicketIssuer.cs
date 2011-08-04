namespace Paramore.Domain.Meetings
{
    public class TicketIssuer : ITicketIssuer
    {
        public Tickets Issue(Capacity capacity)
        {
            return new Tickets(capacity);
        }
    }
}