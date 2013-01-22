using Paramore.Domain.Venues;

namespace Paramore.Domain.Meetings
{
    public interface IAmAnOverbookingPolicy
    {
        Tickets AllocateTickets(Capacity capacity);
    }
}