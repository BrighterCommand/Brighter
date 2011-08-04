using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;
namespace Paramore.Domain.Meetings
{
    public class Scheduler : IScheduler
    {
        private readonly IOverbookingPolicy _overbookingPolicy;

        public Scheduler(IOverbookingPolicy _overbookingPolicy)
        {
            this._overbookingPolicy = _overbookingPolicy;
        }

        public Meeting Schedule(Id meetingId, MeetingDate on, Id location, Id speaker, Capacity capacity)
        {
            var tickets = _overbookingPolicy.AllocateTickets(capacity);
            
            return new Meeting(meeting: on, location: location, speaker: speaker, tickets: tickets, version: new Version(), id: meetingId);

        }
    }
}