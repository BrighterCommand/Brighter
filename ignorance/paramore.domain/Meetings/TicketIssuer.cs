namespace Paramore.Domain.Meetings
{
    internal class TicketIssuer : ITicketIssuer
    {
        public Tickets Issue(Capacity capacity)
        {
            return new Tickets(capacity);
        }
    }
}