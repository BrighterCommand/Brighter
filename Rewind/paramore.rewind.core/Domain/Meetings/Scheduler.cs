using System;
using Paramore.Rewind.Core.Adapters.Repositories;
using Paramore.Rewind.Core.Domain.Venues;
using Version = Paramore.Rewind.Core.Adapters.Repositories.Version;

namespace Paramore.Rewind.Core.Domain.Meetings
{
    public class Scheduler : IScheduler
    {
        private readonly IAmAnOverbookingPolicy _overbookingPolicy;

        public Scheduler(IAmAnOverbookingPolicy _overbookingPolicy)
        {
            this._overbookingPolicy = _overbookingPolicy;
        }

        public Meeting Schedule(Id meetingId, MeetingDate on, Id venue, Id speaker, Capacity capacity)
        {
            if (on == null)
                throw new ArgumentNullException("on", "A meeting must have a date to be scheduled");

            Tickets tickets = _overbookingPolicy.AllocateTickets(capacity);

            var meeting = new Meeting(on, venue, speaker, tickets, new Version(), meetingId);
            meeting.OpenForRegistration();
            return meeting;
        }
    }
}