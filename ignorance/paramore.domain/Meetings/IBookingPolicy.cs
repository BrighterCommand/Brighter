namespace Paramore.Domain.Meetings
{
    public interface IOverbookingPolicy
    {
        Tickets AllocateTickets(Capacity capacity);
    }
}