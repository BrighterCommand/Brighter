using Paramore.Domain.Entities.Meetings;
using Paramore.Domain.ValueTypes;

namespace Paramore.Domain.Factories
{
    public interface IIssueTickets
    {
        Tickets Issue(Capacity capacity);
    }
}