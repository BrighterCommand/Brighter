namespace Paramore.Domain.Meetings
{
    public interface IIssueTickets
    {
        Tickets Issue(Capacity capacity);
    }
}