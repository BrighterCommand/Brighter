namespace Paramore.Domain.Meetings
{
    public interface ITicketIssuer
    {
        Tickets Issue(Capacity capacity);
    }
}