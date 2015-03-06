using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public interface IScheduler
    {
        Meeting Schedule(Id meetingId, MeetingDate on, Id venue, Id speaker, Capacity capacity);
    }
}