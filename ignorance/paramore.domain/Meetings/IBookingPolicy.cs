namespace Paramore.Domain.Meetings
{
    public interface IBookingPolicy
    {
        Tickets AllocateTickets(Capacity capacity);
    }
}