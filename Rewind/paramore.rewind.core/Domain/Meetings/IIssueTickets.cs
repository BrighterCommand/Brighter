using Paramore.Rewind.Core.Domain.Venues;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public interface IIssueTickets
    {
        Tickets Issue(Capacity capacity);
    }
}