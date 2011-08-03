namespace Paramore.Domain.Meetings
{
    internal interface ITicketIssuer
    {
        Tickets Issue(Capacity capacity);
    }
}