namespace Paramore.Domain.Meetings
{
    internal interface IOverbookingPolicy
    {
        Tickets AllocateTickets(Capacity capacity);
    }
}