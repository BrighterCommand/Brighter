using Paramore.Infrastructure.Domain;

namespace Paramore.Domain.Meetings
{
    public interface IScheduler
    {
        Meeting Schedule(Id meetingId, MeetingDate on, Id location, Id speaker, Capacity capacity);
    }
}