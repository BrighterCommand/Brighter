using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;
namespace Paramore.Domain.Meetings
{
    public class Scheduler : IScheduler
    {
        private readonly IBookingPolicy _bookingPolicy;

        public Scheduler(IBookingPolicy bookingPolicy)
        {
            _bookingPolicy = bookingPolicy;
        }

        public Meeting Schedule(Id meetingId, MeetingDate on, Id location, Id speaker, Capacity capacity)
        {
            var tickets = _bookingPolicy.AllocateTickets(capacity);
            
            return new Meeting(meeting: on, location: location, speaker: speaker, tickets: tickets, version: new Version(), id: meetingId);

        }
    }
}