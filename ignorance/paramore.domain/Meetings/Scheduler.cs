using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;
namespace Paramore.Domain.Meetings
{
    public class Scheduler : IScheduler
    {
        private readonly IAmAnOverbookingPolicy _overbookingPolicy;

        public Scheduler(IAmAnOverbookingPolicy _overbookingPolicy)
        {
            this._overbookingPolicy = _overbookingPolicy;
        }

        public Meeting Schedule(Id meetingId, MeetingDate on, Id location, Id speaker, Capacity capacity)
        {
            var tickets = _overbookingPolicy.AllocateTickets(capacity);
            
            return new Meeting(meetingDate: on, venue: location, speaker: speaker, tickets: tickets, version: new Version(), id: meetingId);

        }
    }
}