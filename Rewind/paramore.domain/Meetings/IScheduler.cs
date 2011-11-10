using Paramore.Infrastructure.Domain;

namespace Paramore.Domain.Meetings
{
    public interface IScheduler
    {
        Meeting Schedule(Id meetingId, MeetingDate on, Id venue, Id speaker, Capacity capacity);
    }
}