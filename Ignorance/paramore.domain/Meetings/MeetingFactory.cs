using Paramore.Infrastructure.Domain;
using Version = Paramore.Infrastructure.Domain.Version;
namespace Paramore.Domain.Meetings
{
    public class MeetingFactory : IMeetingFactory
    {
        public Meeting Schedule(Id meetingId, MeetingDate on, Id location, Id speaker, Capacity capacity)
        {
            return new Meeting(meeting: on, location: location, speaker: speaker, capacity: capacity, version: new Version(), id: meetingId);
        }
    }
}