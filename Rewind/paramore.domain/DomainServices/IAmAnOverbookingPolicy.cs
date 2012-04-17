using Paramore.Domain.Entities.Meetings;
using Paramore.Domain.ValueTypes;

namespace Paramore.Domain.DomainServices
{
    public interface IAmAnOverbookingPolicy
    {
        Tickets AllocateTickets(Capacity capacity);
    }
}