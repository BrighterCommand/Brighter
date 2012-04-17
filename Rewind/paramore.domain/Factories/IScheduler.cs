using Paramore.Domain.Entities.Meetings;
using Paramore.Domain.ValueTypes;
using Paramore.Infrastructure.Repositories;

namespace Paramore.Domain.Factories
{
    public interface IScheduler
    {
        Meeting Schedule(Id meetingId, MeetingDate on, Id venue, Id speaker, Capacity capacity);
    }
}